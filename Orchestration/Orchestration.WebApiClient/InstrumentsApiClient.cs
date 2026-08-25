using System.Net;
using System.Net.Http.Json;
using Orchestration.Domain.Models;
using Orchestration.WebApiClient.Core;

namespace Orchestration.WebApiClient;

public class InstrumentsApiClient(HttpClient httpClient) : IInstrumentsApiClient
{
    public async Task<IReadOnlyList<Instrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        var instruments = await httpClient.GetFromJsonAsync<List<Instrument>>("Instruments", cancellationToken);
        return instruments ?? [];
    }

    public async Task<Instrument?> GetInstrumentAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"Instruments/{instrumentId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Instrument>(cancellationToken);
    }

    public async Task<Instrument> AddInstrumentAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("Instruments", instrument, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Instrument>(cancellationToken))!;
    }

    public async Task<Instrument?> UpdateInstrumentAsync(Guid instrumentId, Instrument instrument, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"Instruments/{instrumentId}", instrument, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Instrument>(cancellationToken);
    }
}
