using HospitalInformationSystem.Domain.Diagnoses.Services;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.UpdateDiagnosis;

public sealed class UpdateDiagnosisCommandHandler(
    IDiagnosisRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<UpdateDiagnosisCommand, DiagnosisDefinitionDto>
{
    public async Task<DiagnosisDefinitionDto> Handle(UpdateDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var diagnosis = await repository.FindAsync(request.Id, tracking: true, cancellationToken)
            ?? throw new EntityNotFoundException("Диагноз не найден.");
        DiagnosisDefinitionService.Update(diagnosis, request.Code, request.Name, timeProvider.GetUtcNow());
        if (await repository.ExistsConflictAsync(diagnosis.Code, diagnosis.Name, diagnosis.Id, cancellationToken))
            throw new BusinessConflictException("Диагноз с таким кодом или названием уже существует.");
        await repository.UpdateAsync(diagnosis, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return diagnosis.ToDto();
    }
}

