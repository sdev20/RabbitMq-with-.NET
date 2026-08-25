using InstrumentsApp.Domain;

namespace InstrumentApp.DomainServices.Core;

public interface IInstrumentService
{
    Task<IEnumerable<Instrument>> GetInstruments();
}