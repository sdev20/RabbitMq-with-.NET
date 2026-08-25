using RabbitMQ.Client;

namespace RabbitMessaging.Core;

public interface IRabbitConnectionProvider
{
    Task<IChannel> CreateChannelAsync(string connectionName);
}
