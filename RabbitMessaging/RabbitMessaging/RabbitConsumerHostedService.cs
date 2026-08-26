using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMessaging;

public class RabbitConsumerHostedService<T>(
    IRabbitConnectionProvider connectionProvider,
    RabbitSettings rabbitSettings,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitConsumerHostedService<T>> logger) : BackgroundService where T : class
{
    private const string RetryCountHeader = "x-retry-count";

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messageName = typeof(T).Name;

        var consumerSettings = rabbitSettings.Consumers.FirstOrDefault(c => c.Name == messageName)
            ?? throw new InvalidOperationException($"No Rabbit consumer configured for message '{messageName}'.");

        _channel = await connectionProvider.CreateChannelAsync(consumerSettings.Connection);
        await _channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, deliverEventArgs) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(deliverEventArgs.Body.Span);
                var message = JsonConvert.DeserializeObject<T>(json);

                if (message is not null)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor<T>>();
                    await processor.ProcessAsync(message, stoppingToken);
                }

                await _channel.BasicAckAsync(deliverEventArgs.DeliveryTag, false, stoppingToken);
            }
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
                        logger.LogWarning(
                            "{MessageName} exceeded {MaxRetryCount} retries — dead-lettering instead of requeueing.",
                            messageName, consumerSettings.MaxRetryCount);

                        await _channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                        return;
                    }

                    // Nack/requeue redelivers the message unchanged — it can't carry an
                    // updated header. To track attempts, republish a copy with the
                    // incremented header back through the same exchange/routing key this
                    // delivery arrived through, then ack the original away.
                    headers[RetryCountHeader] = retryCount;

                    await _channel.BasicPublishAsync(
                        exchange: deliverEventArgs.Exchange,
                        routingKey: deliverEventArgs.RoutingKey,
                        mandatory: false,
                        basicProperties: new BasicProperties
                        {
                            Headers = headers,
                            Persistent = deliverEventArgs.BasicProperties.Persistent
                        },
                        body: deliverEventArgs.Body,
                        cancellationToken: stoppingToken);

                    await _channel.BasicAckAsync(deliverEventArgs.DeliveryTag, false, stoppingToken);

                    logger.LogInformation("{MessageName} requeued for retry {RetryCount} of {MaxRetryCount}.",
                        messageName, retryCount, consumerSettings.MaxRetryCount);
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx,
                        "Failed to apply retry/dead-letter policy for {MessageName} — falling back to a plain requeue.",
                        messageName);
                    await _channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, requeue: true, stoppingToken);
                }
            }
        };

        await _channel.BasicConsumeAsync(consumerSettings.Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
