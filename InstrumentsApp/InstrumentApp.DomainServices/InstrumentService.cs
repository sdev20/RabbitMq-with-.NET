using InstrumentApp.DomainServices.Core;
using InstrumentsApp.Domain;
using Orchestration.WebApiClient.Core;

namespace InstrumentApp.DomainServices;

public class InstrumentService(IInstrumentsApiClient instrumentsApiClient) : IInstrumentService
{
    public async Task<IEnumerable<Instrument>> GetInstruments()
    {
        var instruments = await instrumentsApiClient.GetInstrumentsAsync();

        return instruments.Select(i => new Instrument(i.InstrumentId, i.Name, i.Description, i.Status.ToString()));
    }
}