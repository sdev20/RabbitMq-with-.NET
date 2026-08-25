using System.Text;
using InstrumentApp.Infrastructure.Rabbit.Core;
using InstrumentApp.Infrastructure.Rabbit.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InstrumentApp.Infrastructure.Rabbit;

public class RabbitConsumerHostedService<T>(
    IRabbitConnectionProvider connectionProvider,
    RabbitSettings rabbitSettings,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitConsumerHostedService<T>> logger) : BackgroundService where T : class
{
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
                await _channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, requeue: true, stoppingToken);
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
