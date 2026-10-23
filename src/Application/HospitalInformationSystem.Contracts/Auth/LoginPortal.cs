using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Auth;

[JsonConverter(typeof(JsonStringEnumConverter<LoginPortal>))]
public enum LoginPortal
{
    [JsonStringEnumMemberName("medical-worker")]
    MedicalWorker,

    [JsonStringEnumMemberName("administrator")]
    Administrator
}
