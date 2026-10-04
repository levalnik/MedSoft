using HospitalInformationSystem.Domain.Diagnoses;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Infrastructure.Persistence.Mappings;

[Mapper]
internal static partial class DiagnosisDefinitionDboMapper
{
    public static partial DiagnosisDefinition ToDomain(this DiagnosisDefinitionDbo dbo);
    public static partial DiagnosisDefinitionDbo ToDbo(this DiagnosisDefinition entity);

    [MapperIgnoreSource(nameof(DiagnosisDefinition.Id))]
    [MapperIgnoreSource(nameof(DiagnosisDefinition.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(DiagnosisDefinitionDbo.Id))]
    [MapperIgnoreTarget(nameof(DiagnosisDefinitionDbo.CreatedAtUtc))]
    public static partial void UpdateDbo(this DiagnosisDefinition entity, DiagnosisDefinitionDbo dbo);
}

