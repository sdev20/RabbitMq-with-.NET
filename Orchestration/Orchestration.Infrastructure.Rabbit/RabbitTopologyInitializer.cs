using Orchestration.Infrastructure.Rabbit.Core;
using Orchestration.Infrastructure.Rabbit.Models;

namespace Orchestration.Infrastructure.Rabbit;

public class RabbitTopologyInitializer(IRabbitConnectionProvider connectionProvider, RabbitSettings rabbitSettings) : IRabbitTopologyInitializer
{
    public async Task DeclareTopologyAsync()
    {
        foreach (var exchange in rabbitSettings.Schema.Exchanges)
        {
            await using var channel = await connectionProvider.CreateChannelAsync(exchange.Connection);

            await channel.ExchangeDeclareAsync(exchange: exchange.Name,
                type: exchange.Type.ToLowerInvariant(),
                durable: exchange.Durable,
                autoDelete: false,
                arguments: null);
        }

        foreach (var queue in rabbitSettings.Schema.Queues)
        {
            await using var channel = await connectionProvider.CreateChannelAsync(queue.Connection);

            await channel.QueueDeclareAsync(queue: queue.Name,
                durable: queue.Durable,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        foreach (var binding in rabbitSettings.Schema.Bindings)
        {
            var queue = rabbitSettings.Schema.Queues.FirstOrDefault(q => q.Name == binding.Queue)
                ?? throw new InvalidOperationException($"No Rabbit queue configured with name '{binding.Queue}'.");

            await using var channel = await connectionProvider.CreateChannelAsync(queue.Connection);

            await channel.QueueBindAsync(queue: binding.Queue,
                exchange: binding.Exchange,
                routingKey: binding.RoutingKey,
                arguments: null);
        }
    }
}
