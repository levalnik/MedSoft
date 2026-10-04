using HospitalInformationSystem.Domain.Diagnoses.Factories;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Domain.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;

public sealed class CreateDiagnosisCommandHandler(
    IDiagnosisRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<CreateDiagnosisCommand, DiagnosisDefinitionDto>
{
    public async Task<DiagnosisDefinitionDto> Handle(CreateDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var diagnosis = DiagnosisDefinitionFactory.Create(request.Code, request.Name, timeProvider.GetUtcNow());
        if (await repository.ExistsConflictAsync(diagnosis.Code, diagnosis.Name, null, cancellationToken))
            throw new BusinessConflictException("Диагноз с таким кодом или названием уже существует.");
        repository.Add(diagnosis);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return diagnosis.ToDto();
    }
}

