using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

public sealed class AuditEventTypeJsonConverter() : JsonStringEnumConverter<AuditEventType>(
    namingPolicy: null, allowIntegerValues: false);
