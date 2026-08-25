using InstrumentApp.Infrastructure.Rabbit.Messages;

namespace InstrumentsApp.Services.Notifications;

public interface IInstrumentStatusNotifier
{
    event Func<InstrumentStatusChangeMessage, Task>? StatusChanged;

    Task NotifyAsync(InstrumentStatusChangeMessage message);
}
