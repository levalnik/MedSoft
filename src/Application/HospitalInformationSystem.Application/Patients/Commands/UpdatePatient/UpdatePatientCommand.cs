using HospitalInformationSystem.Contracts.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.UpdatePatient;

public sealed record UpdatePatientCommand(
    Guid Id, string FirstName, string LastName, string? MiddleName,
    DateOnly BirthDate, AdministrativeSex Sex, string? Comment) : IRequest<PatientDto>;

