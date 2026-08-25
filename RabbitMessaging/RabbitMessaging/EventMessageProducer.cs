using System.Text;
using Newtonsoft.Json;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;
using RabbitMQ.Client;

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

        await channel.BasicPublishAsync(exchange: exchange.Name,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: new ReadOnlyMemory<byte>(body));

        return $"Published {messageName} to exchange '{exchange.Name}' with routing key '{routingKey}'";
    }
}
