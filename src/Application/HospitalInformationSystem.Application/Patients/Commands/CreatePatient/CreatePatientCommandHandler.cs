using HospitalInformationSystem.Domain.Patients.Factories;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Domain.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.CreatePatient;

public sealed class CreatePatientCommandHandler(
    IPatientRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<CreatePatientCommand, PatientDto>
{
    public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = PatientFactory.Create(request.FirstName, request.LastName, request.MiddleName,
            request.BirthDate, request.Sex.ToDomain(), request.Comment, timeProvider.GetUtcNow());
        repository.Add(patient);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return patient.ToDto();
    }
}

