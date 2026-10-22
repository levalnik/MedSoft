using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.Gateways;

public interface IMedicalRecordsGateway
{
    Task<MedicalRecordDto?> FindForPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<MedicalRecordDto> CreateAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task AddDiagnosisAsync(Guid recordId, Guid diagnosisId, CancellationToken cancellationToken = default);
    Task RemoveDiagnosisAsync(Guid recordId, Guid medicalRecordDiagnosisId,
        CancellationToken cancellationToken = default);
    Task AddPrescriptionAsync(Guid recordId, string text, CancellationToken cancellationToken = default);
    Task CancelPrescriptionAsync(Guid recordId, Guid prescriptionId, CancellationToken cancellationToken = default);
    Task ReplacePrescriptionAsync(Guid recordId, Guid prescriptionId, string text,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MedicalRecordHistoryEventDto>> GetHistoryAsync(Guid recordId,
        CancellationToken cancellationToken = default);
    Task<MedicalRecordSnapshotDto> GetSnapshotAsync(Guid recordId, DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
