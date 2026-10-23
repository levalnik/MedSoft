using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

[JsonConverter(typeof(MedicalWorkerRoleJsonConverter))]
public enum MedicalWorkerRole
{
    Doctor,
    LaboratoryTechnician,
    Nurse,
    Other
}
