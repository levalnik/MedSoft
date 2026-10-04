using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;

public sealed record CreateDiagnosisCommand(string Code, string Name) : IRequest<DiagnosisDefinitionDto>;

