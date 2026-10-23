using System.Net;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;

namespace HospitalInformationSystem.Web.Gateways;

public sealed class MedicalRecordsGateway(MedicalRecordsApiClient client, SessionState session) : IMedicalRecordsGateway
{
    public Task<MedicalRecordDto?> FindForPatientAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        client.FindForPatientAsync(patientId, cancellationToken);

    public Task<MedicalRecordDto> CreateAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        client.CreateAsync(patientId, RequireAuthor(creating: true), cancellationToken);

    public Task AddDiagnosisAsync(Guid recordId, Guid diagnosisId, CancellationToken cancellationToken = default) =>
        client.AddDiagnosisAsync(recordId, diagnosisId, RequireAuthor(), cancellationToken);

    public Task RemoveDiagnosisAsync(Guid recordId, Guid medicalRecordDiagnosisId,
        CancellationToken cancellationToken = default) =>
        client.RemoveDiagnosisAsync(recordId, medicalRecordDiagnosisId, RequireAuthor(), cancellationToken);

    public Task AddPrescriptionAsync(Guid recordId, string text, CancellationToken cancellationToken = default) =>
        client.AddPrescriptionAsync(recordId, text, RequireAuthor(), cancellationToken);

    public Task CancelPrescriptionAsync(Guid recordId, Guid prescriptionId,
        CancellationToken cancellationToken = default) =>
        client.CancelPrescriptionAsync(recordId, prescriptionId, RequireAuthor(), cancellationToken);

    public Task ReplacePrescriptionAsync(Guid recordId, Guid prescriptionId, string text,
        CancellationToken cancellationToken = default) =>
        client.ReplacePrescriptionAsync(recordId, prescriptionId, text, RequireAuthor(), cancellationToken);

    public Task<IReadOnlyList<MedicalRecordHistoryEventDto>> GetHistoryAsync(Guid recordId,
        CancellationToken cancellationToken = default) => client.GetHistoryAsync(recordId, cancellationToken);

    public Task<MedicalRecordSnapshotDto> GetSnapshotAsync(Guid recordId, DateTimeOffset at,
        CancellationToken cancellationToken = default) => client.GetSnapshotAsync(recordId, at, cancellationToken);

    private Guid RequireAuthor(bool creating = false)
    {
        if (session.Current is not { } current)
            throw new ApiException("Для изменения карты необходимо войти в систему.", HttpStatusCode.Unauthorized);
        if (current.Role != ApplicationRoles.MedicalWorker || current.MedicalWorkerId is not { } id || id == Guid.Empty)
            throw new ApiException("Изменять карту может только медработник с действующей учётной записью.",
                HttpStatusCode.Forbidden);
        if (current.MedicalWorkerRole is { } role && !(creating
                ? role is MedicalWorkerRole.Doctor or MedicalWorkerRole.Nurse
                : role == MedicalWorkerRole.Doctor))
            throw new ApiException(creating ? "У текущей медицинской роли нет права создавать ЭМК."
                : "Изменять содержимое ЭМК может только врач.", HttpStatusCode.Forbidden);
        return id;
    }
}
