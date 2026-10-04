using HospitalInformationSystem.Domain.MedicalRecords;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IMedicalRecordRepository
{
    Task<MedicalRecord?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<MedicalRecord?> FindByPatientAsync(Guid patientId, CancellationToken cancellationToken);
    void Add(MedicalRecord record);
    // Обновляется агрегат, ранее прочитанный с tracking: true в этом Unit of Work.
    Task UpdateAsync(MedicalRecord record, CancellationToken cancellationToken);
}
