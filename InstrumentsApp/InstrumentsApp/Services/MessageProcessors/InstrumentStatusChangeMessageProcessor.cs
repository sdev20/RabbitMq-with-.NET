using InstrumentApp.Infrastructure.Rabbit.Core;
using InstrumentApp.Infrastructure.Rabbit.Messages;
using InstrumentsApp.Services.Notifications;

namespace InstrumentsApp.Services.MessageProcessors;

public class InstrumentStatusChangeMessageProcessor(
    IInstrumentStatusNotifier notifier,
    ILogger<InstrumentStatusChangeMessageProcessor> logger) : IMessageProcessor<InstrumentStatusChangeMessage>
{
    public async Task ProcessAsync(InstrumentStatusChangeMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Received status change for instrument {InstrumentId} ({Name}): {Status} at {ChangedAtUtc}",
            message.InstrumentId, message.Name, message.Status, message.ChangedAtUtc);

        await notifier.NotifyAsync(message);
    }
}
