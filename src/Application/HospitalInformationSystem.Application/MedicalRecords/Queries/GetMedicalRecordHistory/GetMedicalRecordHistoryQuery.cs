using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordHistory;

public sealed record GetMedicalRecordHistoryQuery(Guid Id) : IRequest<IReadOnlyList<MedicalRecordHistoryEventDto>>;
