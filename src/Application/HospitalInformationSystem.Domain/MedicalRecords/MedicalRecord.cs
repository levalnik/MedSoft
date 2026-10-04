namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecord
{
    public Guid Id { get; init; }
    public Guid PatientId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public long Version { get; set; }
    public IReadOnlyCollection<MedicalRecordDiagnosis> Diagnoses { get; set; } = [];
    public IReadOnlyCollection<Prescription> Prescriptions { get; set; } = [];
    public IReadOnlyCollection<MedicalRecordHistoryEvent> HistoryEvents { get; set; } = [];
}
