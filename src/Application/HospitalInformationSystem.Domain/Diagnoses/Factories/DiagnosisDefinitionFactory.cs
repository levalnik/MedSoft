using HospitalInformationSystem.Domain.Diagnoses.Services;

namespace HospitalInformationSystem.Domain.Diagnoses.Factories;

public static class DiagnosisDefinitionFactory
{
    public static DiagnosisDefinition Create(string code, string name, DateTimeOffset now)
    {
        var diagnosis = new DiagnosisDefinition
        {
            Id = Guid.NewGuid(), CreatedAtUtc = now.ToUniversalTime(),
            Code = string.Empty, Name = string.Empty
        };
        DiagnosisDefinitionService.Update(diagnosis, code, name, now);
        return diagnosis;
    }
}
