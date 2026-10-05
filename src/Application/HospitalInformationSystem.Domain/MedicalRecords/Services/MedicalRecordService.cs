using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalRecords.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Domain.MedicalRecords.Services;

public static class MedicalRecordService
{
    public static MedicalRecordDiagnosis AddDiagnosis(
        MedicalRecord record, Guid diagnosisDefinitionId, string code, string name, MedicalWorker author,
        DateTimeOffset now)
    {
        ValidateAuthor(author);
        var at = EventTime(record, now);
        var diagnosis =
            MedicalRecordDiagnosisFactory.Create(record.Id, diagnosisDefinitionId, code, name, author.Id, at);
        record.Diagnoses = record.Diagnoses.Append(diagnosis).ToList().AsReadOnly();
        AppendEvent(record, MedicalRecordEventType.DiagnosisAdded, diagnosis.Id,
            new DiagnosisAddedPayload(diagnosis.Id, diagnosisDefinitionId, diagnosis.DiagnosisCodeSnapshot,
                diagnosis.DiagnosisNameSnapshot), author, at);
        return diagnosis;
    }

    public static void RemoveDiagnosis(MedicalRecord record, Guid itemId, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        var diagnosis = record.Diagnoses.SingleOrDefault(x => x.Id == itemId)
                        ?? throw new DomainEntityNotFoundException("Диагноз в этой медицинской карте не найден.");
        if (diagnosis.RemovedAtUtc is not null)
            throw new BusinessRuleConflictException("Диагноз уже удалён из медицинской карты.");
        var at = EventTime(record, now);
        diagnosis.RemovedAtUtc = at;
        diagnosis.RemovedByMedicalWorkerId = author.Id;
        AppendEvent(record, MedicalRecordEventType.DiagnosisRemoved, diagnosis.Id,
            new DiagnosisRemovedPayload(diagnosis.Id, diagnosis.DiagnosisCodeSnapshot, diagnosis.DiagnosisNameSnapshot),
            author, at);
    }

    public static Prescription AddPrescription(MedicalRecord record, string text, MedicalWorker author,
        DateTimeOffset now)
    {
        ValidateAuthor(author);
        var at = EventTime(record, now);
        var prescription = PrescriptionFactory.Create(record.Id, text, author.Id, at);
        AppendPrescription(record, prescription, author, at);
        return prescription;
    }

    public static void CancelPrescription(MedicalRecord record, Guid itemId, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        CancelPrescriptionCore(record, GetActivePrescription(record, itemId), author, EventTime(record, now));
    }

    public static Prescription ReplacePrescription(MedicalRecord record, Guid itemId, string text, MedicalWorker author,
        DateTimeOffset now)
    {
        ValidateAuthor(author);
        var original = GetActivePrescription(record, itemId);
        var at = EventTime(record, now);
        var replacement = PrescriptionFactory.Create(record.Id, text, author.Id, at, original.Id);
        CancelPrescriptionCore(record, original, author, at);
        AppendPrescription(record, replacement, author, at);
        return replacement;
    }

    private static Prescription GetActivePrescription(MedicalRecord record, Guid itemId)
    {
        var prescription = record.Prescriptions.SingleOrDefault(x => x.Id == itemId)
                           ?? throw new DomainEntityNotFoundException(
                               "Назначение в этой медицинской карте не найдено.");
        if (prescription.CancelledAtUtc is not null)
            throw new BusinessRuleConflictException("Назначение уже отменено.");
        return prescription;
    }

    private static void AppendPrescription(MedicalRecord record, Prescription prescription, MedicalWorker author,
        DateTimeOffset at)
    {
        record.Prescriptions = record.Prescriptions.Append(prescription).ToList().AsReadOnly();
        AppendEvent(record, MedicalRecordEventType.PrescriptionAdded, prescription.Id,
            new PrescriptionAddedPayload(prescription.Id, prescription.Text, prescription.ReplacesPrescriptionId),
            author, at);
    }

    private static void CancelPrescriptionCore(MedicalRecord record, Prescription prescription, MedicalWorker author,
        DateTimeOffset at)
    {
        prescription.CancelledAtUtc = at;
        prescription.CancelledByMedicalWorkerId = author.Id;
        AppendEvent(record, MedicalRecordEventType.PrescriptionCancelled, prescription.Id,
            new PrescriptionCancelledPayload(prescription.Id, prescription.Text), author, at);
    }

    private static void AppendEvent<TPayload>(MedicalRecord record, MedicalRecordEventType type, Guid? entityId,
        TPayload payload,
        MedicalWorker author, DateTimeOffset at)
    {
        var version = record.Version + 1;
        var historyEvent =
            MedicalRecordHistoryEventFactory.Create(record.Id, version, type, entityId, payload, author, at);
        record.HistoryEvents = record.HistoryEvents.Append(historyEvent).ToList().AsReadOnly();
        record.Version = version;
    }

    private static DateTimeOffset EventTime(MedicalRecord record, DateTimeOffset now)
    {
        var previous = record.HistoryEvents.MaxBy(x => x.Version)?.OccurredAtUtc ?? record.CreatedAtUtc;
        var requested = now.ToUniversalTime();
        return requested < previous ? previous : requested;
    }

    internal static void ValidateAuthor(MedicalWorker author)
    {
        if (author.IsDeleted)
            throw new DomainValidationException("Автор изменения должен быть действующим медработником.");
    }
}
