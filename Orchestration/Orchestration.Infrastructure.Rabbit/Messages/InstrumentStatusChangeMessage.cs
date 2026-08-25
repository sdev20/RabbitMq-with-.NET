using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Orchestration.Domain.Enums;

namespace Orchestration.Infrastructure.Rabbit.Messages;

public record InstrumentStatusChangeMessage(
    Guid InstrumentId,
    string Name,
    [property: JsonConverter(typeof(StringEnumConverter))] InstrumentStatus Status,
    DateTimeOffset ChangedAtUtc);
