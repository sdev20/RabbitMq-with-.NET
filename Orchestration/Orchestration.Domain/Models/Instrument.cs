using Orchestration.Domain.Enums;

namespace Orchestration.Domain.Models;

public record Instrument(Guid InstrumentId, string Name, string Description, InstrumentStatus Status);