using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.AddMedicalRecordDiagnosis;

public sealed record AddMedicalRecordDiagnosisCommand(Guid MedicalRecordId, Guid DiagnosisId, Guid MedicalWorkerId)
    : IRequest<MedicalRecordDiagnosisDto>;
