using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Security;

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
