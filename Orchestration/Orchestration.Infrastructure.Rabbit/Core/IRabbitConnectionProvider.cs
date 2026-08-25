using RabbitMQ.Client;

namespace Orchestration.Infrastructure.Rabbit.Core;

public interface IRabbitConnectionProvider
{
    Task<IChannel> CreateChannelAsync(string connectionName);
}
