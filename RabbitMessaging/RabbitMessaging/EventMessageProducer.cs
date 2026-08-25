using System.Text;
using Newtonsoft.Json;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace RabbitMessaging;

public class EventMessageProducer<T>(IRabbitConnectionProvider connectionProvider, RabbitSettings rabbitSettings) : IEventMessageProducer<T> where T : class
{
    public async Task<string> PublishAsync(T item, string routingKey)
    {
        var messageName = typeof(T).Name;

        var publisher = rabbitSettings.Publishers.FirstOrDefault(p => p.Name == messageName)
            ?? throw new InvalidOperationException($"No Rabbit publisher configured for message '{messageName}'.");

        var exchange = rabbitSettings.Schema.Exchanges.FirstOrDefault(e => e.Name == publisher.Exchange)
            ?? throw new InvalidOperationException($"No Rabbit exchange configured with name '{publisher.Exchange}'.");

        await using var channel = await connectionProvider.CreateChannelAsync(publisher.Connection);

        var json = JsonConvert.SerializeObject(item);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = exchange.Durable
        };

        // mandatory: true means an unroutable message (routing key matches no queue) is
        // rejected instead of silently dropped. Combined with publisher confirms enabled
        // on the channel (see RabbitConnectionProvider), BasicPublishAsync doesn't complete
        // until the broker actually acks the message — a failure surfaces as an exception
        // here instead of the caller wrongly believing the publish succeeded.
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
            // More specific than PublishException (which it derives from) — the broker
            // accepted the message but couldn't route it anywhere, e.g. a typo'd routing
            // key or a binding that doesn't exist yet.
            throw new InvalidOperationException(
                $"Message '{messageName}' was unroutable — no queue is bound to exchange '{exchange.Name}' " +
                $"with routing key '{routingKey}' (broker replied {ex.ReplyCode}: {ex.ReplyText}).", ex);
        }
        catch (PublishException ex)
        {
            throw new InvalidOperationException(
                $"Broker did not confirm message '{messageName}' published to exchange '{exchange.Name}' " +
                $"(publish sequence {ex.PublishSequenceNumber}).", ex);
        }

        return $"Published and confirmed {messageName} to exchange '{exchange.Name}' with routing key '{routingKey}'";
    }
}
