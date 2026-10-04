using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.DeleteDiagnosis;

public sealed record DeleteDiagnosisCommand(Guid Id) : IRequest;

