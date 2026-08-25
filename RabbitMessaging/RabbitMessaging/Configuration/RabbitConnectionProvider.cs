using System.Collections.Concurrent;
using Polly;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace RabbitMessaging.Configuration;

public class RabbitConnectionProvider(RabbitSettings rabbitSettings) : IRabbitConnectionProvider, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<IConnection>>> _connections = new();

    public async Task<IChannel> CreateChannelAsync(string connectionName)
    {
        var connectionLazy = _connections.GetOrAdd(connectionName,
            name => new Lazy<Task<IConnection>>(() => OpenConnectionWithRetryAsync(name)));

        IConnection connection;
        try
        {
            connection = await connectionLazy.Value;
        }
        catch
        {
            // A Lazy<Task<T>> caches a faulted task forever — without this, one failed
            // attempt would permanently poison this connection name, and no later retry
            // (background or otherwise) would ever get a fresh attempt. Evict it so the
            // next call starts over instead of replaying the same cached failure.
            _connections.TryRemove(connectionName, out _);
            throw;
        }

        return await connection.CreateChannelAsync();
    }

    private Task<IConnection> OpenConnectionWithRetryAsync(string connectionName)
    {
        var settings = rabbitSettings.Connections.FirstOrDefault(c => c.Name == connectionName)
                       ?? throw new InvalidOperationException(
                           $"No Rabbit connection configured with name '{connectionName}'.");

        var factory = new ConnectionFactory
        {
            HostName = settings.Server,
            UserName = settings.UserName,
            Password = settings.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        // Define the exponential retry policy.
        // Must be the async variant: Policy.Execute (sync) only wraps the synchronous act of
        // *starting* an async lambda — it gets back a Task immediately, before the connection
        // attempt has actually succeeded or failed, so it never sees the eventual exception and
        // never retries. WaitAndRetryAsync + ExecuteAsync await the operation inside the policy,
        // so a fault is actually caught while Polly is still watching for it.
        var retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s, 16s, 32s
                onRetry: (exception, timeSpan, attempt, context) =>
                {
                    Console.WriteLine(
                        $"Attempt {attempt} failed. Retrying in {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                }
            );

        return retryPolicy.ExecuteAsync(() => factory.CreateConnectionAsync());
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
