using HospitalInformationSystem.Domain.MedicalWorkers.Services;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.DeleteMedicalWorker;

public sealed class DeleteMedicalWorkerCommandHandler(
    IMedicalWorkerRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteMedicalWorkerCommand>
{
    public async Task Handle(DeleteMedicalWorkerCommand request, CancellationToken cancellationToken)
    {
        var worker = await repository.FindAsync(request.Id, tracking: true, cancellationToken)
                     ?? throw new EntityNotFoundException("Медработник не найден.");
        MedicalWorkerService.MarkAsDeleted(worker, timeProvider.GetUtcNow());
        await repository.UpdateAsync(worker, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
