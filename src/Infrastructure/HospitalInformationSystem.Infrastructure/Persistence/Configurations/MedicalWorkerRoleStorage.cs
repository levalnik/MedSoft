using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

internal static class MedicalWorkerRoleStorage
{
    public static string Write(MedicalWorkerRole role) => role switch
    {
        MedicalWorkerRole.Doctor => "doctor",
        MedicalWorkerRole.LaboratoryTechnician => "laboratory-technician",
        MedicalWorkerRole.Nurse => "nurse",
        MedicalWorkerRole.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static MedicalWorkerRole Read(string role) => role switch
    {
        "doctor" => MedicalWorkerRole.Doctor,
        "laboratory-technician" => MedicalWorkerRole.LaboratoryTechnician,
        "nurse" => MedicalWorkerRole.Nurse,
        "other" => MedicalWorkerRole.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}
