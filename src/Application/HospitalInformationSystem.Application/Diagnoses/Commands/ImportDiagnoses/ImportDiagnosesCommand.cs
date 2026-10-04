using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.ImportDiagnoses;

public sealed record ImportDiagnosesCommand(string Csv) : IRequest<DiagnosisImportResultDto>;
