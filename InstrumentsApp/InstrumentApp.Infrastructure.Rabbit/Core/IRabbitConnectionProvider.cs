using RabbitMQ.Client;

namespace InstrumentApp.Infrastructure.Rabbit.Core;

public interface IRabbitConnectionProvider
{
    Task<IChannel> CreateChannelAsync(string connectionName);
}
