using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMessaging.Core;

namespace RabbitMessaging;

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

                try
                {
                    await Task.Delay(RetryInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
