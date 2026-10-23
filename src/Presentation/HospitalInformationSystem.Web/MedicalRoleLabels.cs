using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web;

public static class MedicalRoleLabels
{
    public static string Describe(MedicalWorkerRole? role)
    {
        if (role is not { } value)
        {
            return "—";
        }

        return value switch
        {
            MedicalWorkerRole.Doctor => "Врач",
            MedicalWorkerRole.Nurse => "Медсестра / медбрат",
            MedicalWorkerRole.LaboratoryTechnician => "Лаборант",
            MedicalWorkerRole.Other => "Другая роль",
            _ => "—"
        };
    }
}
