using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;

public sealed class UpdateMedicalWorkerCommandHandler(
    IMedicalWorkerRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<UpdateMedicalWorkerCommand, MedicalWorkerDto>
{
    public async Task<MedicalWorkerDto> Handle(UpdateMedicalWorkerCommand request, CancellationToken cancellationToken)
    {
        var worker = await repository.FindAsync(request.Id, tracking: true, cancellationToken)
            ?? throw new EntityNotFoundException("Медработник не найден.");
        worker.Update(request.FirstName, request.LastName, request.Position, timeProvider.GetUtcNow());
        await repository.UpdateAsync(worker, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return worker.ToDto();
    }
}
