using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Application.MedicalRecords.Snapshot;

public sealed class MedicalRecordSnapshotBuilder(IMedicalRecordRepository repository) : IMedicalRecordSnapshotBuilder
{
    public async Task<MedicalRecordSnapshotDto> BuildAsync(
        Guid medicalRecordId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var record = await repository.FindAsync(medicalRecordId, tracking: false, cancellationToken)
                     ?? throw new EntityNotFoundException("Медицинская карта не найдена.");
        return MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, at);
    }
}
