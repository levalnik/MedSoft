using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

[JsonConverter(typeof(AuditActorKindJsonConverter))]
public enum AuditActorKind
{
    User,
    Anonymous,
    System
}
