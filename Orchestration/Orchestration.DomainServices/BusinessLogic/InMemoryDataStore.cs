using Orchestration.Domain.Enums;
using Orchestration.Domain.Models;

namespace Orchestration.DomainServices.BusinessLogic;

public class InMemoryDataStore
{
    private readonly Lock _lock = new();

    private readonly List<Instrument> _instruments =
    [
        new Instrument(Guid.NewGuid(), "Micropipettes", "transfer tiny, exact volumes of liquid", InstrumentStatus.Available),
        new Instrument(Guid.NewGuid(), "Spectrophotometers", "measure the concentration of biomolecules.", InstrumentStatus.Available),
        new Instrument(Guid.NewGuid(), "PCR Machines", "amplify DNA sequences", InstrumentStatus.Available),
        new Instrument(Guid.NewGuid(), "Centrifuges", "Spin samples at high speeds", InstrumentStatus.Unavailable),
        new Instrument(Guid.NewGuid(), "Electrophoresis Units", "electrical charge to separate DNA, RNA, or proteins", InstrumentStatus.Available)
    ];

    public List<Instrument> GetInstruments()
    {
        lock (_lock)
        {
            return [.._instruments];
        }
    }

    public Instrument? GetInstrument(Guid instrumentId)
    {
        lock (_lock)
        {
            return _instruments.FirstOrDefault(i => i.InstrumentId == instrumentId);
        }
    }

    public Instrument AddInstrument(Instrument instrument)
    {
        lock (_lock)
        {
            _instruments.Add(instrument);
            return instrument;
        }
    }

    public Instrument? UpdateInstrument(Instrument instrument)
    {
        lock (_lock)
        {
            var index = _instruments.FindIndex(i => i.InstrumentId == instrument.InstrumentId);
            if (index == -1)
            {
                return null;
            }

            _instruments[index] = instrument;
            return instrument;
        }
    }
}
