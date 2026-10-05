using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.DeletePatient;

public sealed class DeletePatientCommandHandler(
    IPatientRepository repository,
    IMedicalRecordPresence recordPresence,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePatientCommand>
{
    public async Task Handle(DeletePatientCommand request, CancellationToken cancellationToken)
    {
        _ = await repository.FindAsync(request.Id, tracking: true, cancellationToken)
            ?? throw new EntityNotFoundException("Пациент не найден.");
        if (await recordPresence.ExistsForPatientAsync(request.Id, cancellationToken))
            throw new BusinessConflictException(
                "Нельзя удалить пациента, у которого есть электронная медицинская карта.");
        await repository.DeleteAsync(request.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
