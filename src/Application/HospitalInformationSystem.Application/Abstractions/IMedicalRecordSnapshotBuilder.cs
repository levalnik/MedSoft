using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IMedicalRecordSnapshotBuilder
{
    Task<MedicalRecordSnapshotDto> BuildAsync(Guid medicalRecordId, DateTimeOffset at, CancellationToken cancellationToken);
}
