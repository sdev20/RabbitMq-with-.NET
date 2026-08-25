using Orchestration.Domain.Models;

namespace Orchestration.WebApiClient.Core;

public interface IInstrumentsApiClient
{
    Task<IReadOnlyList<Instrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default);

    Task<Instrument?> GetInstrumentAsync(Guid instrumentId, CancellationToken cancellationToken = default);

    Task<Instrument> AddInstrumentAsync(Instrument instrument, CancellationToken cancellationToken = default);

    Task<Instrument?> UpdateInstrumentAsync(Guid instrumentId, Instrument instrument, CancellationToken cancellationToken = default);
}
