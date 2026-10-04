using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordSnapshot;

public sealed record GetMedicalRecordSnapshotQuery(Guid Id, DateTimeOffset At) : IRequest<MedicalRecordSnapshotDto>;
