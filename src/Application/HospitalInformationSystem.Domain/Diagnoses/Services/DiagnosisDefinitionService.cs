using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.Diagnoses.Services;

public static class DiagnosisDefinitionService
{
    public static void Update(DiagnosisDefinition diagnosis, string code, string name, DateTimeOffset now)
    {
        var validCode = code?.Trim();
        var validName = name?.Trim();
        if (validCode?.Length != 6)
            throw new DomainValidationException("Код диагноза: требуется ровно 6 символов.");
        if (validName is null || validName.Length is < 3 or > 24)
            throw new DomainValidationException("Название диагноза: требуется от 3 до 24 символов.");
        diagnosis.Code = validCode;
        diagnosis.Name = validName;
        diagnosis.UpdatedAtUtc = now.ToUniversalTime();
    }
}
