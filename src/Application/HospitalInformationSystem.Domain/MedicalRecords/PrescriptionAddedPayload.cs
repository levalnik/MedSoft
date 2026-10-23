namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed record PrescriptionAddedPayload(Guid PrescriptionId, string Text, Guid? ReplacesPrescriptionId);
