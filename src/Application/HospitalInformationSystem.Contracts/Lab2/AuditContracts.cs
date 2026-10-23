using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Lab2;

[JsonConverter(typeof(AuditEventTypeJsonConverter))]
public enum AuditEventType
{
    AdministratorLogin,
    UserLogin,
    LoginFailed,
    MedicalWorkerCreated,
    MedicalWorkerUpdated,
    MedicalWorkerDeleted,
    PatientCreated,
    PatientUpdated,
    PatientDeleted,
    MedicalRecordCreated,
    MedicalRecordChanged,
    AccountCreated,
    AccountChanged,
    AdministratorSetup,
    UserLogout,
    SessionRefreshed
}

public sealed class AuditEventTypeJsonConverter() : JsonStringEnumConverter<AuditEventType>(
    namingPolicy: null, allowIntegerValues: false);

[JsonConverter(typeof(AuditActorKindJsonConverter))]
public enum AuditActorKind
{
    User,
    Anonymous,
    System
}

public sealed class AuditActorKindJsonConverter() : JsonStringEnumConverter<AuditActorKind>(
    JsonNamingPolicy.CamelCase, allowIntegerValues: false);

public sealed record AuditActorDto(
    AuditActorKind Kind,
    Guid? UserId,
    string DisplayName,
    string? RequestId,
    string? Component);

public sealed record AuditEventDto(
    Guid EventId,
    DateTimeOffset EventTime,
    AuditEventType EventType,
    AuditActorDto Actor,
    string Message,
    IReadOnlyDictionary<string, JsonElement> Metadata);

public sealed record AuditQuery(
    int Page = 1,
    int PageSize = 50,
    AuditEventType? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
