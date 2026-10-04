namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class MedicalRecordDbo
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long Version { get; set; }
    public List<MedicalRecordDiagnosisDbo> Diagnoses { get; set; } = [];
    public List<PrescriptionDbo> Prescriptions { get; set; } = [];
    public List<MedicalRecordHistoryEventDbo> HistoryEvents { get; set; } = [];
}

