using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalRecords.Services;
using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Domain.MedicalRecords.Factories;

public static class MedicalRecordFactory
{
    public static MedicalRecord Create(Guid patientId, MedicalWorker author, DateTimeOffset now)
    {
        MedicalRecordService.ValidateAuthor(author);
        if (patientId == Guid.Empty)
            throw new DomainValidationException("Не указан пациент.");
        var id = Guid.NewGuid();
        var at = now.ToUniversalTime();
        var created = MedicalRecordHistoryEventFactory.Create(id, 1, MedicalRecordEventType.MedicalRecordCreated,
            id, new MedicalRecordCreatedPayload(patientId), author, at);
        return new MedicalRecord
        {
            Id = id, PatientId = patientId, CreatedAtUtc = at, Version = 1,
            HistoryEvents = Array.AsReadOnly([created])
        };
    }
}
