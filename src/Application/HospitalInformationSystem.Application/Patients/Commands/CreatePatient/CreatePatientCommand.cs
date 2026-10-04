using HospitalInformationSystem.Contracts.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    string FirstName, string LastName, string? MiddleName,
    DateOnly BirthDate, AdministrativeSex Sex, string? Comment) : IRequest<PatientDto>;

