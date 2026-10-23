namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed record PrescriptionCancelledPayload(Guid PrescriptionId, string Text);
