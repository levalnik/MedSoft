using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnoses;

public sealed class GetDiagnosesQueryHandler(IDiagnosisRepository repository)
    : IRequestHandler<GetDiagnosesQuery, IReadOnlyList<DiagnosisDefinitionDto>>
{
    public async Task<IReadOnlyList<DiagnosisDefinitionDto>> Handle(GetDiagnosesQuery request, CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken)).Select(Mapping.DiagnosisMapping.ToDto).ToArray();
}

