using System.Collections.Concurrent;
using Orchestration.Infrastructure.Rabbit.Core;
using Orchestration.Infrastructure.Rabbit.Models;
using RabbitMQ.Client;

namespace Orchestration.Infrastructure.Rabbit;

public class RabbitConnectionProvider(RabbitSettings rabbitSettings) : IRabbitConnectionProvider, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<IConnection>>> _connections = new();

    public async Task<IChannel> CreateChannelAsync(string connectionName)
    {
        var connection = await _connections.GetOrAdd(connectionName,
            name => new Lazy<Task<IConnection>>(() => OpenConnectionAsync(name))).Value;

        return await connection.CreateChannelAsync();
    }

    private Task<IConnection> OpenConnectionAsync(string connectionName)
    {
        var settings = rabbitSettings.Connections.FirstOrDefault(c => c.Name == connectionName)
            ?? throw new InvalidOperationException($"No Rabbit connection configured with name '{connectionName}'.");

        var factory = new ConnectionFactory
        {
            HostName = settings.Server,
            UserName = settings.UserName,
            Password = settings.Password
        };

        return factory.CreateConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var lazyConnection in _connections.Values)
        {
            if (!lazyConnection.IsValueCreated)
            {
                continue;
            }

            var connection = await lazyConnection.Value;
            await connection.DisposeAsync();
        }
    }
}
