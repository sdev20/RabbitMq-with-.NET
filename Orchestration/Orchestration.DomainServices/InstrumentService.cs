using Orchestration.Domain.Models;
using Orchestration.DomainServices.BusinessLogic;
using Orchestration.DomainServices.BusinessLogic.Core;
using Orchestration.Infrastructure.Rabbit.Core;
using Orchestration.Infrastructure.Rabbit.Messages;

namespace Orchestration.DomainServices;

public class InstrumentService(
    InMemoryDataStore dataStore,
    IEventMessageProducer<InstrumentStatusChangeMessage> eventMessageProducer) : IInstrumentService
{
    private const string InstrumentStatusChangedRoutingKey = "instrument.status.changed";

    public List<Instrument> GetInstruments() => dataStore.GetInstruments();

    public Instrument? GetInstrument(Guid instrumentId) => dataStore.GetInstrument(instrumentId);

    public Instrument AddInstrument(Instrument instrument) => dataStore.AddInstrument(instrument);

    public async Task<Instrument?> UpdateInstrument(Instrument instrument)
    {
        var updated = dataStore.UpdateInstrument(instrument);
        if (updated is null)
        {
            return null;
        }

        var message = new InstrumentStatusChangeMessage(updated.InstrumentId, updated.Name, updated.Status, DateTimeOffset.UtcNow);
        await eventMessageProducer.PublishAsync(message, InstrumentStatusChangedRoutingKey);

        return updated;
    }
}
