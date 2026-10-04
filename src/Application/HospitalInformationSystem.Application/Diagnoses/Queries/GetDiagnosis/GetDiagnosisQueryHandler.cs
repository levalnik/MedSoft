using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnosis;

public sealed class GetDiagnosisQueryHandler(IDiagnosisRepository repository)
    : IRequestHandler<GetDiagnosisQuery, DiagnosisDefinitionDto>
{
    public async Task<DiagnosisDefinitionDto> Handle(GetDiagnosisQuery request, CancellationToken cancellationToken)
    {
        var diagnosis = await repository.FindAsync(request.Id, tracking: false, cancellationToken)
            ?? throw new EntityNotFoundException("Диагноз не найден.");
        return diagnosis.ToDto();
    }
}

