using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.ReplacePrescription;

public sealed record ReplacePrescriptionCommand(
    Guid MedicalRecordId,
    Guid PrescriptionId,
    string Text,
    Guid MedicalWorkerId) : IRequest<PrescriptionDto>;
