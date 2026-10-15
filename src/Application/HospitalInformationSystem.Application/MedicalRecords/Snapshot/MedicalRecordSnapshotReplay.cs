using System.Text.Json;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Domain.MedicalRecords;

namespace HospitalInformationSystem.Application.MedicalRecords.Snapshot;

public static class MedicalRecordSnapshotReplay
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static MedicalRecordSnapshotDto Build(
        MedicalRecord record, IEnumerable<MedicalRecordHistoryEvent> historyEvents, DateTimeOffset at)
    {
        at = at.ToUniversalTime();
        if (at < record.CreatedAtUtc)
            throw new EntityNotFoundException("На выбранный момент медицинская карта ещё не существовала.");

        var diagnoses = new Dictionary<Guid, MedicalRecordDiagnosisDto>();
        var prescriptions = new Dictionary<Guid, PrescriptionDto>();
        var diagnosisIds = new HashSet<Guid>();
        var prescriptionIds = new HashSet<Guid>();
        var cancelledPrescriptionIds = new HashSet<Guid>();
        var replacedPrescriptionIds = new HashSet<Guid>();
        var completeHistory = historyEvents.OrderBy(x => x.Version).ToArray();
        MedicalRecordSnapshotDto? snapshot = null;
        var version = 0L;
        var previousTime = record.CreatedAtUtc;

        foreach (var item in completeHistory)
        {
            if (item.OccurredAtUtc > at && snapshot is null)
                snapshot = Capture();
            Require(item.MedicalRecordId == record.Id, "событие принадлежит другой карте");
            Require(item.Version == version + 1, "нарушена последовательность версий");
            Require(item.OccurredAtUtc >= previousTime, "время события меньше времени предыдущего");
            Require(version != 0 || item.EventType == MedicalRecordEventType.MedicalRecordCreated,
                "первое событие должно создавать карту");

            switch (item.EventType)
            {
                case MedicalRecordEventType.MedicalRecordCreated:
                {
                    var payload = Read<MedicalRecordCreatedPayload>(item);
                    Require(
                        version == 0 && payload.PatientId == record.PatientId &&
                        item.OccurredAtUtc == record.CreatedAtUtc,
                        "некорректное событие создания карты");
                    RequireEntityId(item, record.Id);
                    break;
                }
                case MedicalRecordEventType.DiagnosisAdded:
                {
                    var payload = Read<DiagnosisAddedPayload>(item);
                    RequireEntityId(item, payload.MedicalRecordDiagnosisId);
                    Require(payload.DiagnosisDefinitionId != Guid.Empty && !string.IsNullOrWhiteSpace(payload.Code)
                                                                        && !string.IsNullOrWhiteSpace(payload.Name),
                        "неполные данные добавленного диагноза");
                    Require(diagnosisIds.Add(payload.MedicalRecordDiagnosisId),
                        "повторное добавление той же записи диагноза");
                    diagnoses.Add(payload.MedicalRecordDiagnosisId, new MedicalRecordDiagnosisDto(
                        payload.MedicalRecordDiagnosisId, payload.Code, payload.Name, item.OccurredAtUtc,
                        item.PerformedByMedicalWorkerId));
                    break;
                }
                case MedicalRecordEventType.DiagnosisRemoved:
                {
                    var payload = Read<DiagnosisRemovedPayload>(item);
                    RequireEntityId(item, payload.MedicalRecordDiagnosisId);
                    Require(diagnoses.TryGetValue(payload.MedicalRecordDiagnosisId, out var diagnosis),
                        "удаление отсутствующего диагноза");
                    Require(diagnosis!.Code == payload.Code && diagnosis.Name == payload.Name,
                        "изменён исторический диагноз");
                    diagnoses.Remove(payload.MedicalRecordDiagnosisId);
                    break;
                }
                case MedicalRecordEventType.PrescriptionAdded:
                {
                    var payload = Read<PrescriptionAddedPayload>(item);
                    RequireEntityId(item, payload.PrescriptionId);
                    Require(!string.IsNullOrWhiteSpace(payload.Text), "отсутствует текст назначения");
                    Require(prescriptionIds.Add(payload.PrescriptionId), "повторное добавление того же назначения");
                    Require(
                        payload.ReplacesPrescriptionId is null ||
                        (cancelledPrescriptionIds.Contains(payload.ReplacesPrescriptionId.Value) &&
                         replacedPrescriptionIds.Add(payload.ReplacesPrescriptionId.Value)),
                        "заменяемое назначение не было отменено или уже заменено");
                    prescriptions.Add(payload.PrescriptionId, new PrescriptionDto(payload.PrescriptionId, payload.Text,
                        item.OccurredAtUtc, item.PerformedByMedicalWorkerId, payload.ReplacesPrescriptionId));
                    break;
                }
                case MedicalRecordEventType.PrescriptionCancelled:
                {
                    var payload = Read<PrescriptionCancelledPayload>(item);
                    RequireEntityId(item, payload.PrescriptionId);
                    Require(prescriptions.TryGetValue(payload.PrescriptionId, out var prescription),
                        "отмена отсутствующего назначения");
                    Require(prescription!.Text == payload.Text, "изменён исторический текст назначения");
                    prescriptions.Remove(payload.PrescriptionId);
                    cancelledPrescriptionIds.Add(payload.PrescriptionId);
                    break;
                }
                default:
                    throw Corrupted($"неизвестный тип события {item.EventType}");
            }

            version = item.Version;
            previousTime = item.OccurredAtUtc;
        }

        Require(version > 0, "отсутствует событие создания карты");
        Require(version == record.Version, "последняя версия истории не совпадает с версией карты");
        return snapshot ?? Capture();

        MedicalRecordSnapshotDto Capture() => new(record.Id, record.PatientId, at, version,
            diagnoses.Values.ToArray(), prescriptions.Values.ToArray());
    }

    private static T Read<T>(MedicalRecordHistoryEvent item) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(item.PayloadJson, PayloadOptions)
                   ?? throw Corrupted($"пустой payload события версии {item.Version}");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"История медицинской карты повреждена: неверный JSON версии {item.Version}.", exception);
        }
    }

    private static void RequireEntityId(MedicalRecordHistoryEvent item, Guid id) =>
        Require(id != Guid.Empty && item.EntityId == id, "идентификатор события не совпадает с payload");

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw Corrupted(detail);
    }

    private static InvalidOperationException Corrupted(string detail) =>
        new($"История медицинской карты повреждена: {detail}.");
}
