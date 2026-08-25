using InstrumentApp.Infrastructure.Rabbit.Messages;

namespace InstrumentsApp.Services.Notifications;

public class InstrumentStatusNotifier : IInstrumentStatusNotifier
{
    public event Func<InstrumentStatusChangeMessage, Task>? StatusChanged;

    public async Task NotifyAsync(InstrumentStatusChangeMessage message)
    {
        var handlers = StatusChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<InstrumentStatusChangeMessage, Task>>())
        {
            await handler(message);
        }
    }
}
