using System.Text.Json;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecord
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);
    private readonly List<MedicalRecordDiagnosis> _diagnoses;
    private readonly List<Prescription> _prescriptions;
    private readonly List<MedicalRecordHistoryEvent> _historyEvents;

    // Восстановление сохранённого агрегата; новые события возникают только в бизнес-методах.
    public MedicalRecord(
        Guid id,
        Guid patientId,
        DateTimeOffset createdAtUtc,
        long version,
        IReadOnlyCollection<MedicalRecordDiagnosis> diagnoses,
        IReadOnlyCollection<Prescription> prescriptions,
        IReadOnlyCollection<MedicalRecordHistoryEvent> historyEvents)
    {
        Id = id;
        PatientId = patientId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Version = version;
        _diagnoses = diagnoses.ToList();
        _prescriptions = prescriptions.ToList();
        _historyEvents = historyEvents.OrderBy(x => x.Version).ToList();
        Diagnoses = _diagnoses.AsReadOnly();
        Prescriptions = _prescriptions.AsReadOnly();
        HistoryEvents = _historyEvents.AsReadOnly();
    }

    public Guid Id { get; }
    public Guid PatientId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public long Version { get; private set; }
    public IReadOnlyCollection<MedicalRecordDiagnosis> Diagnoses { get; }
    public IReadOnlyCollection<Prescription> Prescriptions { get; }
    public IReadOnlyCollection<MedicalRecordHistoryEvent> HistoryEvents { get; }

    public static MedicalRecord Create(Guid patientId, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        if (patientId == Guid.Empty)
            throw new DomainValidationException("Не указан пациент.");
        var at = now.ToUniversalTime();
        var record = new MedicalRecord(Guid.NewGuid(), patientId, at, 0, [], [], []);
        record.AppendEvent(MedicalRecordEventType.MedicalRecordCreated, record.Id,
            new MedicalRecordCreatedPayload(patientId), author, at);
        return record;
    }

    public MedicalRecordDiagnosis AddDiagnosis(
        Guid diagnosisDefinitionId, string code, string name, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        if (diagnosisDefinitionId == Guid.Empty)
            throw new DomainValidationException("Не указан диагноз справочника.");
        var validCode = code?.Trim();
        var validName = name?.Trim();
        if (validCode is null || validCode.Length != 6)
            throw new DomainValidationException("Код диагноза должен содержать 6 символов.");
        if (validName is null || validName.Length is < 3 or > 24)
            throw new DomainValidationException("Название диагноза: требуется от 3 до 24 символов.");
        var at = EventTime(now);
        var diagnosis = new MedicalRecordDiagnosis(Guid.NewGuid(), Id, diagnosisDefinitionId,
            validCode, validName, at, author.Id, null, null);
        _diagnoses.Add(diagnosis);
        AppendEvent(MedicalRecordEventType.DiagnosisAdded, diagnosis.Id,
            new DiagnosisAddedPayload(diagnosis.Id, diagnosisDefinitionId, validCode, validName), author, at);
        return diagnosis;
    }

    public void RemoveDiagnosis(Guid itemId, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        var diagnosis = _diagnoses.SingleOrDefault(x => x.Id == itemId)
            ?? throw new DomainEntityNotFoundException("Диагноз в этой медицинской карте не найден.");
        if (diagnosis.RemovedAtUtc is not null)
            throw new BusinessRuleConflictException("Диагноз уже удалён из медицинской карты.");
        var at = EventTime(now);
        diagnosis.Remove(author.Id, at);
        AppendEvent(MedicalRecordEventType.DiagnosisRemoved, diagnosis.Id,
            new DiagnosisRemovedPayload(diagnosis.Id, diagnosis.DiagnosisCodeSnapshot, diagnosis.DiagnosisNameSnapshot),
            author, at);
    }

    public Prescription AddPrescription(string text, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        return AddPrescriptionCore(Prescription.ValidateText(text), null, author, EventTime(now));
    }

    public void CancelPrescription(Guid itemId, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        CancelPrescriptionCore(GetActivePrescription(itemId), author, EventTime(now));
    }

    public Prescription ReplacePrescription(Guid itemId, string text, MedicalWorker author, DateTimeOffset now)
    {
        ValidateAuthor(author);
        var original = GetActivePrescription(itemId);
        // Проверяем весь ввод до изменения старой записи: ошибка не оставит её отменённой.
        var validText = Prescription.ValidateText(text);
        var at = EventTime(now);
        CancelPrescriptionCore(original, author, at);
        return AddPrescriptionCore(validText, original.Id, author, at);
    }

    private Prescription GetActivePrescription(Guid itemId)
    {
        var prescription = _prescriptions.SingleOrDefault(x => x.Id == itemId)
            ?? throw new DomainEntityNotFoundException("Назначение в этой медицинской карте не найдено.");
        if (prescription.CancelledAtUtc is not null)
            throw new BusinessRuleConflictException("Назначение уже отменено.");
        return prescription;
    }

    private Prescription AddPrescriptionCore(string text, Guid? replacesId, MedicalWorker author, DateTimeOffset at)
    {
        var prescription = new Prescription(Guid.NewGuid(), Id, text, at, author.Id, null, null, replacesId);
        _prescriptions.Add(prescription);
        AppendEvent(MedicalRecordEventType.PrescriptionAdded, prescription.Id,
            new PrescriptionAddedPayload(prescription.Id, text, replacesId), author, at);
        return prescription;
    }

    private void CancelPrescriptionCore(Prescription prescription, MedicalWorker author, DateTimeOffset at)
    {
        prescription.Cancel(author.Id, at);
        AppendEvent(MedicalRecordEventType.PrescriptionCancelled, prescription.Id,
            new PrescriptionCancelledPayload(prescription.Id, prescription.Text), author, at);
    }

    private void AppendEvent<T>(MedicalRecordEventType type, Guid? entityId, T payload, MedicalWorker author, DateTimeOffset at)
    {
        var payloadJson = JsonSerializer.Serialize(payload, PayloadOptions);
        _historyEvents.Add(new MedicalRecordHistoryEvent(Guid.NewGuid(), Id, ++Version, at, author.Id,
            $"{author.LastName} {author.FirstName}", author.Position, type, entityId, payloadJson));
    }

    private DateTimeOffset EventTime(DateTimeOffset now)
    {
        // Откат системных часов не должен разрывать временной префикс истории при replay.
        var previous = _historyEvents.Count == 0 ? CreatedAtUtc : _historyEvents[^1].OccurredAtUtc;
        var requested = now.ToUniversalTime();
        return requested < previous ? previous : requested;
    }

    private static void ValidateAuthor(MedicalWorker author)
    {
        if (author.IsDeleted)
            throw new DomainValidationException("Автор изменения должен быть действующим медработником.");
    }
}
