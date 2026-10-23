using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

public sealed record AuditQuery(
    int Page = 1,
    int PageSize = 50,
    AuditEventType? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
