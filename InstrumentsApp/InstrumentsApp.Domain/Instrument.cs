namespace InstrumentsApp.Domain;

public record Instrument(Guid InstrumentId, string Name, string Description, string Status);