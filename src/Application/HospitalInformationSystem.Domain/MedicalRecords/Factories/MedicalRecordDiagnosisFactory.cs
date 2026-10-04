using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalRecords.Factories;

public static class MedicalRecordDiagnosisFactory
{
    public static MedicalRecordDiagnosis Create(
        Guid medicalRecordId, Guid diagnosisDefinitionId, string code, string name, Guid authorId, DateTimeOffset at)
    {
        if (diagnosisDefinitionId == Guid.Empty)
            throw new DomainValidationException("Не указан диагноз справочника.");
        var validCode = code?.Trim();
        var validName = name?.Trim();
        if (validCode is null || validCode.Length != 6)
            throw new DomainValidationException("Код диагноза должен содержать 6 символов.");
        if (validName is null || validName.Length is < 3 or > 24)
            throw new DomainValidationException("Название диагноза: требуется от 3 до 24 символов.");
        return new MedicalRecordDiagnosis
        {
            Id = Guid.NewGuid(), MedicalRecordId = medicalRecordId, DiagnosisDefinitionId = diagnosisDefinitionId,
            DiagnosisCodeSnapshot = validCode, DiagnosisNameSnapshot = validName,
            AddedAtUtc = at.ToUniversalTime(), AddedByMedicalWorkerId = authorId
        };
    }
}
