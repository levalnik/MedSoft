using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Patients;

[JsonConverter(typeof(JsonStringEnumConverter<AdministrativeSex>))]
public enum AdministrativeSex
{
    Male,
    Female,
    Unknown
}

