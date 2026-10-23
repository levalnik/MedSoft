using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.Gateways;

public interface IMedicalRecordsGateway
{
    Task<MedicalRecordDto?> FindForPatientAsync(Guid patientId, CancellationToken cancellationToken);
    Task<MedicalRecordDto> CreateAsync(Guid patientId, CancellationToken cancellationToken);
    Task AddDiagnosisAsync(Guid recordId, Guid diagnosisId, CancellationToken cancellationToken);
    Task RemoveDiagnosisAsync(Guid recordId, Guid medicalRecordDiagnosisId,
        CancellationToken cancellationToken);
    Task AddPrescriptionAsync(Guid recordId, string text, CancellationToken cancellationToken);
    Task CancelPrescriptionAsync(Guid recordId, Guid prescriptionId, CancellationToken cancellationToken);
    Task ReplacePrescriptionAsync(Guid recordId, Guid prescriptionId, string text,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MedicalRecordHistoryEventDto>> GetHistoryAsync(Guid recordId,
        CancellationToken cancellationToken);
    Task<MedicalRecordSnapshotDto> GetSnapshotAsync(Guid recordId, DateTimeOffset at,
        CancellationToken cancellationToken);
}
