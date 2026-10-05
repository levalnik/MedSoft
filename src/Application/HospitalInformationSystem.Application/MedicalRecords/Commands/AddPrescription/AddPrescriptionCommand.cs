using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.AddPrescription;

public sealed record AddPrescriptionCommand(Guid MedicalRecordId, string Text, Guid MedicalWorkerId)
    : IRequest<PrescriptionDto>;
