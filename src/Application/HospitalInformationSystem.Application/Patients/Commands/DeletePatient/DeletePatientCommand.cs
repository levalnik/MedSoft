using MediatR;

namespace HospitalInformationSystem.Application.Patients.Commands.DeletePatient;

public sealed record DeletePatientCommand(Guid Id) : IRequest;
