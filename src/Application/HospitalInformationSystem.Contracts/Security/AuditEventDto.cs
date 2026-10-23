using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

public sealed record AuditEventDto(
    Guid EventId,
    DateTimeOffset EventTime,
    AuditEventType EventType,
    AuditActorDto Actor,
    string Message,
    IReadOnlyDictionary<string, JsonElement> Metadata);
