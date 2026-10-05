using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Domain.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;

public sealed class CreateMedicalWorkerCommandHandler(
    IMedicalWorkerRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateMedicalWorkerCommand, MedicalWorkerDto>
{
    public async Task<MedicalWorkerDto> Handle(CreateMedicalWorkerCommand request, CancellationToken cancellationToken)
    {
        var worker = MedicalWorkerFactory.Create(request.FirstName, request.LastName, request.Position,
            timeProvider.GetUtcNow());
        repository.Add(worker);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Mapping.MedicalWorkerMapping.ToDto(worker);
    }
}
