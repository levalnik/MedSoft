using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.RemoveMedicalRecordDiagnosis;

public sealed record RemoveMedicalRecordDiagnosisCommand(Guid MedicalRecordId, Guid MedicalRecordDiagnosisId, Guid MedicalWorkerId) : IRequest;
