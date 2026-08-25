namespace InstrumentApp.Infrastructure.Rabbit.Messages;

public record InstrumentStatusChangeMessage(Guid InstrumentId, string Name, string Status, DateTimeOffset ChangedAtUtc);
