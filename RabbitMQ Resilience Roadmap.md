# RabbitMQ Resilience Roadmap

This document is about making the pipeline survive the failure modes a message broker actually runs into in production. It's the follow-up to [RabbitMQ Implementation with .NET](./RabbitMQ%20Implementation%20with%20.Net.md), worked through one phase at a time.


## Status

| # | Phase | Status |
|---|-------|--------|
| 1 | Verify message durability | Not started |
| 2 | Connection retry with backoff | **Implemented** |
| 3 | Publisher confirms | **Implemented** |
| 4 | Dead-letter queue + consumer retry policy | **Implemented** |
| 5 | Consumer idempotency | Not started |
| 6 | Broker-down UI notification | Not started |
| 7 | Clustering / quorum queues | Not started |


---

## 1. Messages lost during broker downtime

**Concern:** if RabbitMQ restarts (crash, deploy, host reboot), do the messages sitting in a queue survive, or vanish with it?

**RabbitMQ concepts:**
- A **durable** exchange or queue means the broker persists its *definition* to disk — it still exists after a restart.
- A **persistent** message means the message *body* is written to disk when it lands in a durable queue, not just held in memory.

Both are required together. A durable queue holding non-persistent messages still loses everything on restart — durability protects the queue's existence, persistence protects what's inside it.

Both `appsettings.json` files already declare every exchange and queue as durable:

```json
{ "Name": "orchestration.instruments", "Type": "Topic", "Durable": true }
```

And `EventMessageProducer` already marks every published message as persistent, tied to the exchange's own durability:

```csharp
var properties = new BasicProperties { Persistent = exchange.Durable };
```

**What this phase actually is:** proof, not code. Publish a message, stop the broker before InstrumentsApp consumes it, start it back up, confirm the message is still there and gets delivered. If it survives, then it is durable and persistent.

---

## 2. Publisher can't connect to the broker

**Concern:** if the broker isn't reachable when the app starts — or the connection drops mid-session — what happens?

Added Exponential Retry policy in case of connection failure with max retries


```csharp
// RabbitConnectionProvider.cs — Configuration/RabbitConnectionProvider.cs
var retryPolicy = Policy
    .Handle<BrokerUnreachableException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s, 16s, 32s
        onRetry: (exception, timeSpan, attempt, context) =>
        {
            Console.WriteLine($"Attempt {attempt} failed. Retrying in {timeSpan.TotalSeconds}s. Error: {exception.Message}");
        });

return retryPolicy.ExecuteAsync(() => factory.CreateConnectionAsync());
```


**The degraded-start behavior** (start anyway, keep retrying in the background, rather than crash at boot): `Program.cs` used to block on `DeclareTopologyAsync()` before `app.Run()`, so an unreachable broker at startup crashed the whole app. That blocking call is replaced with a `BackgroundService` that retries indefinitely for every 30s:

```csharp
// RabbitTopologyBackgroundInitializer.cs
public class RabbitTopologyBackgroundInitializer(
    IRabbitTopologyInitializer topologyInitializer,
    ILogger<RabbitTopologyBackgroundInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await topologyInitializer.DeclareTopologyAsync();
                logger.LogInformation("Rabbit topology declared successfully.");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Could not declare Rabbit topology — RabbitMQ may be unreachable. The app stays up, " +
                    "but publishing/consuming will fail until this succeeds. Retrying in {RetryInterval}.",
                    RetryInterval);

                try { await Task.Delay(RetryInterval, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
```

`BackgroundService.StartAsync` doesn't block the host on `ExecuteAsync` finishing — it kicks the loop off and returns immediately — so `Program.cs` just registers it and moves on:

```csharp
builder.Services.AddHostedService<RabbitTopologyBackgroundInitializer>();
// no more: await app.Services.GetRequiredService<IRabbitTopologyInitializer>().DeclareTopologyAsync();
```

**Net effect:** broker down at startup → app starts and serves traffic immediately, retries every 30s in the background, picks up automatically once the broker's reachable, no restart needed.

---

## 3. Publisher can't be sure a message was actually accepted — Implemented

**Concern:** `BasicPublishAsync` is "fire and forget" by default — no built-in way to know whether the broker actually received, routed, and persisted the message.

**Enabled publisher confirms on every channel** `RabbitConnectionProvider` creates:

```csharp
var channelOptions = new CreateChannelOptions(
    publisherConfirmationsEnabled: true,
    publisherConfirmationTrackingEnabled: true);

return await connection.CreateChannelAsync(channelOptions);
```

These are two different switches, not one:
- **`publisherConfirmationsEnabled`** is the *protocol-level* switch — it tells the broker to start sending ack/nack frames back for this channel's publishes at all. Without it, the broker never confirms anything, no matter what the client does.
- **`publisherConfirmationTrackingEnabled`** is the *client-library* switch — it makes the client correlate each ack/nack back to the specific `BasicPublishAsync` call that sent it, and complete or fault that call's `Task` accordingly. With both on, `await BasicPublishAsync(...)` genuinely doesn't return until that message is confirmed.

Harmless to enable on every channel, including consume-only ones — it's inert unless something actually calls `BasicPublishAsync`.

**`mandatory: true`**, so an unroutable message (routing key matches no queue — a typo'd binding) is rejected instead of silently dropped, plus handling for both ways a publish can now fail:

```csharp
// EventMessageProducer.cs
try
{
    await channel.BasicPublishAsync(exchange: exchange.Name,
        routingKey: routingKey,
        mandatory: true,
        basicProperties: properties,
        body: new ReadOnlyMemory<byte>(body));
}
catch (PublishReturnException ex)
{
    // More specific than PublishException (which it derives from) — broker accepted
    // the message but couldn't route it anywhere.
    throw new InvalidOperationException(
        $"Message '{messageName}' was unroutable — no queue is bound to exchange '{exchange.Name}' " +
        $"with routing key '{routingKey}' (broker replied {ex.ReplyCode}: {ex.ReplyText}).", ex);
}
catch (PublishException ex)
{
    // Broker explicitly nacked the message — accepted it, but failed to persist/route
    // it internally (rare — e.g. an internal broker error).
    throw new InvalidOperationException(
        $"Broker did not confirm message '{messageName}' published to exchange '{exchange.Name}' " +
        $"(publish sequence {ex.PublishSequenceNumber}).", ex);
}

return $"Published and confirmed {messageName} to exchange '{exchange.Name}' with routing key '{routingKey}'";
```

`PublishReturnException` is caught first because it derives from `PublishException` — the more specific case has to come before the general one or it'd never be reached.

**Net effect:** a failed publish now throws instead of the caller wrongly believing it succeeded.

---

## 4. Consumer can't process messages loop forever -- Dead Letter Handling

**Concern:** What if consumer fails to process the message?
Every message processing failure does `BasicNackAsync(..., requeue: true)` unconditionally — a permanently malformed message, or a genuine bug in a processor, redelivered forever ( which is putting it back to queue and redelivering to the consumer indefinitely or until message TTL)


**Dead-letter handling.** 

RabbitMQ's newer quorum queue type maintains a per-message x-delivery-count header automatically, incrementing on every redelivery — no app-level header bookkeeping needed, replacing the custom x-retry-count logic above entirely. 

But implemented manual retry method here:

**Manual per-message retry counter.** A plain `nack(requeue: true)` redelivers the *exact same* message — no header changes, no counter. To count attempts, the consumer has to track its own header and, since a redelivery can't carry a modified header, **republish** a new copy with the counter incremented rather than just requeueing.

<img width="835" height="663" alt="image" src="https://github.com/user-attachments/assets/740841dd-5755-4d77-88d5-21fadeedc061" />


**Config additions** — a queue can now declare where to send messages it gives up on, and a consumer can set its own retry limit:

```csharp
// RabbitQueueSettings.cs
public string? DeadLetterExchange { get; set; }

// RabbitConsumerSettings.cs
public int MaxRetryCount { get; set; } = 5;
```

**`RabbitTopologyInitializer`** passes that through as a native RabbitMQ queue argument at declare time:

```csharp
var arguments = queue.DeadLetterExchange is not null
    ? new Dictionary<string, object?> { ["x-dead-letter-exchange"] = queue.DeadLetterExchange }
    : null;

await channel.QueueDeclareAsync(queue: queue.Name, durable: queue.Durable,
    exclusive: false, autoDelete: false, arguments: arguments);
```

**The retry/dead-letter logic**, in `RabbitConsumerHostedService<T>`'s failure handler:

```csharp
private const string RetryCountHeader = "x-retry-count";
...
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process {MessageName} message", messageName);

    try
    {
        var headers = deliverEventArgs.BasicProperties.Headers is not null
            ? new Dictionary<string, object?>(deliverEventArgs.BasicProperties.Headers)
            : new Dictionary<string, object?>();

        var retryCount = headers.TryGetValue(RetryCountHeader, out var raw) && raw is not null
            ? Convert.ToInt32(raw) + 1
            : 1;

        if (retryCount > consumerSettings.MaxRetryCount)
        {
            logger.LogWarning("{MessageName} exceeded {MaxRetryCount} retries — dead-lettering instead of requeueing.",
                messageName, consumerSettings.MaxRetryCount);

            await _channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, requeue: false, stoppingToken);
            return;
        }

        // Nack/requeue redelivers the message unchanged — it can't carry an updated
        // header. To track attempts, republish a copy with the incremented header back
        // through the same exchange/routing key this delivery arrived through, then ack
        // the original away.
        headers[RetryCountHeader] = retryCount;

        await _channel.BasicPublishAsync(
            exchange: deliverEventArgs.Exchange,
            routingKey: deliverEventArgs.RoutingKey,
            mandatory: false,
            basicProperties: new BasicProperties { Headers = headers, Persistent = deliverEventArgs.BasicProperties.Persistent },
            body: deliverEventArgs.Body,
            cancellationToken: stoppingToken);

        await _channel.BasicAckAsync(deliverEventArgs.DeliveryTag, false, stoppingToken);
    }
    catch (Exception retryEx)
    {
        // If the retry/dead-letter bookkeeping itself fails, fall back to a plain
        // requeue rather than losing the message.
        logger.LogError(retryEx, "Failed to apply retry/dead-letter policy for {MessageName} — falling back to a plain requeue.", messageName);
        await _channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, requeue: true, stoppingToken);
    }
}
```

Once `MaxRetryCount` is exceeded, the code doesn't publish to the dead-letter queue itself — it just `nack`s with `requeue: false`, and RabbitMQ routes the message to the configured `x-dead-letter-exchange` automatically.

**Wiring it up** — InstrumentsApp's `appsettings.json` declares a fanout dead-letter exchange, a DLQ bound to it, and points the source queue and consumer at them:

```json
"Schema": {
  "Exchanges": [
    { "Name": "orchestration.instruments", "Connection": "InstrumentsAppRabbitConsumer", "Type": "Topic", "Durable": true },
    { "Name": "instruments-app.instrument-status-changed.dlx", "Connection": "InstrumentsAppRabbitConsumer", "Type": "Fanout", "Durable": true }
  ],
  "Queues": [
    { "Name": "instruments-app.instrument-status-changed", "Connection": "InstrumentsAppRabbitConsumer", "Durable": true,
      "DeadLetterExchange": "instruments-app.instrument-status-changed.dlx" },
    { "Name": "instruments-app.instrument-status-changed.dlq", "Connection": "InstrumentsAppRabbitConsumer", "Durable": true }
  ],
  "Bindings": [
    { "Exchange": "orchestration.instruments", "Queue": "instruments-app.instrument-status-changed", "RoutingKey": "instrument.status.changed" },
    { "Exchange": "instruments-app.instrument-status-changed.dlx", "Queue": "instruments-app.instrument-status-changed.dlq", "RoutingKey": "" }
  ]
},
"Consumers": [
  { "Name": "InstrumentStatusChangeMessage", "Connection": "InstrumentsAppRabbitConsumer",
    "Queue": "instruments-app.instrument-status-changed", "MaxRetryCount": 5 }
]
```

A fanout exchange ignores the routing key entirely, which is exactly what a DLX needs — forward everything to the one DLQ regardless of the original routing key.

**How DLQs are handled in the real world — and why the DLQ can carry stale data:**

A DLQ is a triage inbox, not an auto-retry mechanism. The standard posture: something alerts on DLQ depth, a human (or a semi-automated tool) looks at *why* it failed, and only then decides whether to replay, fix-and-replay, or discard. Blindly replaying everything in a DLQ back onto the original queue is a known anti-pattern — a message that failed at 2pm can be stale by 5pm if newer messages for the same entity succeeded in between; naive replay would silently overwrite current-correct state with old data.

The more robust real answer is to make replay *safe by construction* rather than relying on discipline: if every message carries something ordering-comparable (a timestamp, a version, a sequence number), the consumer's processing logic becomes "only apply this if it's newer than what I already have." Under that rule, replaying a stale DLQ message is naturally a no-op instead of data corruption. `InstrumentStatusChangeMessage` already carries `ChangedAtUtc` — that's exactly the field a last-write-wins check would use. This is why phase 5 (idempotency) is the natural next step after phase 4, not a nice-to-have — it's what makes DLQ recovery actually safe. DLQs also typically carry their own TTL in production (old, untriaged messages auto-expire) rather than growing forever.


---


