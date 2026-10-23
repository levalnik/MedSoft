using System.Net;
using System.Text.Json;
using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.Development;

public sealed partial class UiMockHttpMessageHandler
{
    private HttpResponseMessage PatientRecord(HttpRequestMessage request, Guid patientId)
    {
        if (!state.Patients.TryGetValue(patientId, out var patient)) return NotFound();
        var existing = state.Records.Values.FirstOrDefault(x => x.Current.PatientId == patientId);
        if (request.Method == HttpMethod.Get) return existing is null ? NotFound() : Json(existing.Current);
        if (request.Method != HttpMethod.Post) return NotFound();
        if (state.CurrentMedicalRole is not ("doctor" or "nurse")) return Forbidden();
        if (ValidateAuthor(request) is { } error) return error;
        if (existing is not null) return Error(HttpStatusCode.Conflict, "У пациента уже есть ЭМК.");
        var at = DateTimeOffset.UtcNow;
        var record = new MockMedicalRecord(new MedicalRecordDto(Guid.NewGuid(), patientId,
            at, 0, [], []));
        state.Records.Add(record.Current.Id, record);
        state.Patients[patientId] = patient with { HasMedicalRecord = true };
        state.AppendEvent(record, "MedicalRecordCreated", null, "{}", at);
        state.AddAudit("MedicalRecordCreated", "Создана демонстрационная ЭМК.", record.Current.Id);
        return Json(record.Current, HttpStatusCode.Created);
    }

    private HttpResponseMessage MedicalRecords(HttpRequestMessage request, string[] path, string? body)
    {
        if (state.IsAdministrator) return Forbidden();
        if (path.Length < 3 || !state.Records.TryGetValue(Id(path[2]), out var record)) return NotFound();
        if (request.Method == HttpMethod.Get)
        {
            if (path.Length == 3) return Json(record.Current);
            if (path.Length != 4) return NotFound();
            if (path[3] == "history") return Json(record.History.ToArray());
            if (path[3] != "snapshot") return NotFound();
            var at = Date(Query(request.RequestUri!), "at");
            if (at is null) return BadRequest("Укажите момент времени с часовым поясом.");
            var snapshot = record.States.LastOrDefault(x => x.At <= at);
            return snapshot.State is null ? NotFound() : Json(new MedicalRecordSnapshotDto(record.Current.Id,
                record.Current.PatientId, at.Value, snapshot.State.Version,
                snapshot.State.Diagnoses, snapshot.State.Prescriptions));
        }
        if (state.CurrentMedicalRole != "doctor") return Forbidden();
        if (ValidateAuthor(request) is { } error) return error;
        var now = DateTimeOffset.UtcNow;
        var authorId = state.CurrentSession!.MedicalWorkerId!.Value;
        HttpResponseMessage? response = null;
        if (path.Length == 4 && path[3] == "diagnoses" && request.Method == HttpMethod.Post)
        {
            var input = Read<AddMedicalRecordDiagnosisRequest>(body);
            if (!state.Diagnoses.TryGetValue(input.DiagnosisId, out var diagnosis)) return NotFound();
            var item = new MedicalRecordDiagnosisDto(Guid.NewGuid(), diagnosis.Code, diagnosis.Name, now, authorId);
            record.Current = record.Current with { Diagnoses = record.Current.Diagnoses.Append(item).ToArray() };
            state.AppendEvent(record, "DiagnosisAdded", item.Id, Payload(new { item.Id, item.Code, item.Name }), now);
            response = Json(item, HttpStatusCode.Created);
        }
        else if (path.Length == 5 && path[3] == "diagnoses" && request.Method == HttpMethod.Delete)
        {
            var item = record.Current.Diagnoses.FirstOrDefault(x => x.Id == Id(path[4]));
            if (item is null) return NotFound();
            record.Current = record.Current with
                { Diagnoses = record.Current.Diagnoses.Where(x => x.Id != item.Id).ToArray() };
            state.AppendEvent(record, "DiagnosisRemoved", item.Id, Payload(new { item.Id, item.Code, item.Name }), now);
        }
        else if (path.Length == 4 && path[3] == "prescriptions" && request.Method == HttpMethod.Post)
        {
            var input = Read<SavePrescriptionRequest>(body);
            if (!ValidText(input.Text, 3, 128)) return BadRequest("Текст назначения: 3–128 символов.");
            var item = new PrescriptionDto(Guid.NewGuid(), input.Text.Trim(), now, authorId, null);
            record.Current = record.Current with { Prescriptions = record.Current.Prescriptions.Append(item).ToArray() };
            state.AppendEvent(record, "PrescriptionAdded", item.Id,
                Payload(new { item.Id, item.Text, item.ReplacesPrescriptionId }), now);
            response = Json(item, HttpStatusCode.Created);
        }
        else if (path.Length == 5 && path[3] == "prescriptions" && request.Method == HttpMethod.Put)
        {
            var old = record.Current.Prescriptions.FirstOrDefault(x => x.Id == Id(path[4]));
            if (old is null) return NotFound();
            var input = Read<SavePrescriptionRequest>(body);
            if (!ValidText(input.Text, 3, 128)) return BadRequest("Текст назначения: 3–128 символов.");
            record.Current = record.Current with
                { Prescriptions = record.Current.Prescriptions.Where(x => x.Id != old.Id).ToArray() };
            state.AppendEvent(record, "PrescriptionCancelled", old.Id, Payload(new { old.Id, old.Text }), now);
            var replacement = new PrescriptionDto(Guid.NewGuid(), input.Text.Trim(), now, authorId, old.Id);
            record.Current = record.Current with
                { Prescriptions = record.Current.Prescriptions.Append(replacement).ToArray() };
            state.AppendEvent(record, "PrescriptionAdded", replacement.Id,
                Payload(new { replacement.Id, replacement.Text, replacement.ReplacesPrescriptionId }), now);
            response = Json(replacement);
        }
        else if (path.Length == 6 && path[3] == "prescriptions" && path[5] == "cancel"
            && request.Method == HttpMethod.Post)
        {
            var item = record.Current.Prescriptions.FirstOrDefault(x => x.Id == Id(path[4]));
            if (item is null) return NotFound();
            record.Current = record.Current with
                { Prescriptions = record.Current.Prescriptions.Where(x => x.Id != item.Id).ToArray() };
            state.AppendEvent(record, "PrescriptionCancelled", item.Id, Payload(new { item.Id, item.Text }), now);
        }
        else return NotFound();
        state.AddAudit("MedicalRecordChanged", "Изменена демонстрационная ЭМК.", record.Current.Id);
        return response ?? NoContent();
    }

    private HttpResponseMessage? ValidateAuthor(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("X-Medical-Worker-Id", out var values)) return null;
        var array = values.ToArray();
        if (array.Length != 1 || !Guid.TryParse(array[0], out var authorId) || authorId == Guid.Empty)
            return BadRequest("Некорректный автор изменения.");
        return authorId == state.CurrentSession?.MedicalWorkerId ? null : Forbidden();
    }

    private static string Payload(object value) => JsonSerializer.Serialize(value, JsonOptions);
}
