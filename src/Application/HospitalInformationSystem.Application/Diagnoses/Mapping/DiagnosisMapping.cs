using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Domain.Diagnoses;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Application.Diagnoses.Mapping;

[Mapper]
internal static partial class DiagnosisMapping
{
    [MapperIgnoreSource(nameof(DiagnosisDefinition.CreatedAtUtc))]
    [MapperIgnoreSource(nameof(DiagnosisDefinition.UpdatedAtUtc))]
    public static partial DiagnosisDefinitionDto ToDto(this DiagnosisDefinition diagnosis);
}
