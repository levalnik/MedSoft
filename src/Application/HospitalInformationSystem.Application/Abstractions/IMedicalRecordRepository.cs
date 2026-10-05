using HospitalInformationSystem.Domain.MedicalRecords;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IMedicalRecordRepository
{
    Task<MedicalRecord?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<MedicalRecord?> FindByPatientAsync(Guid patientId, CancellationToken cancellationToken);
    void Add(MedicalRecord record);
    Task UpdateAsync(MedicalRecord record, CancellationToken cancellationToken);
}
