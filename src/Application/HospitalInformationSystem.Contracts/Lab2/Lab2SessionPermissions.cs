using HospitalInformationSystem.Contracts.Auth;

namespace HospitalInformationSystem.Contracts.Lab2;

public static class Lab2SessionPermissions
{
    public static bool CanManageAdministration(AuthenticationSessionDto? session) =>
        session?.AccountRole == ApplicationRoles.Administrator;

    public static bool CanReadClinicalData(AuthenticationSessionDto? session) =>
        session is { AccountRole: ApplicationRoles.MedicalWorker, MedicalWorkerId: { } id,
            MedicalWorkerRole: { } role } && id != Guid.Empty && Enum.IsDefined(role);

    public static bool CanManagePatients(AuthenticationSessionDto? session) =>
        CanReadClinicalData(session) && session!.MedicalWorkerRole is MedicalWorkerRole.Doctor or MedicalWorkerRole.Nurse;

    public static bool CanCreateMedicalRecord(AuthenticationSessionDto? session) => CanManagePatients(session);

    public static bool CanEditMedicalRecord(AuthenticationSessionDto? session) =>
        CanReadClinicalData(session) && session!.MedicalWorkerRole == MedicalWorkerRole.Doctor;
}
