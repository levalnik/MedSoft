namespace HospitalInformationSystem.Contracts.MedicalRecords;

public sealed record PrescriptionDto(
    Guid Id,
    string Text,
    DateTimeOffset CreatedAtUtc,
    Guid CreatedByMedicalWorkerId,
    Guid? ReplacesPrescriptionId);
