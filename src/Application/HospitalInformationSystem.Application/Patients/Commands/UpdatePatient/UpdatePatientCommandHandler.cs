using HospitalInformationSystem.Domain.Patients.Services;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.UpdatePatient;

public sealed class UpdatePatientCommandHandler(
    IPatientRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IMedicalRecordPresence recordPresence)
    : IRequestHandler<UpdatePatientCommand, PatientDto>
{
    public async Task<PatientDto> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.FindAsync(request.Id, tracking: true, cancellationToken)
                      ?? throw new EntityNotFoundException("Пациент не найден.");
        PatientService.Update(patient, request.FirstName, request.LastName, request.MiddleName,
            request.BirthDate, Mapping.PatientMapping.ToDomain(request.Sex), request.Comment, timeProvider.GetUtcNow());
        await repository.UpdateAsync(patient, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Mapping.PatientMapping.ToDto(patient) with
        {
            HasMedicalRecord = await recordPresence.ExistsForPatientAsync(patient.Id, cancellationToken)
        };
    }
}
