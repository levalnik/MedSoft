using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.UpdateDiagnosis;

public sealed record UpdateDiagnosisCommand(Guid Id, string Code, string Name) : IRequest<DiagnosisDefinitionDto>;

