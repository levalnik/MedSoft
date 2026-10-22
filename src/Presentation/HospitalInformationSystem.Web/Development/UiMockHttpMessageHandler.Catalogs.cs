using System.Net;
using System.Text;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;

namespace HospitalInformationSystem.Web.Development;

public sealed partial class UiMockHttpMessageHandler
{
    private HttpResponseMessage MedicalWorkers(HttpRequestMessage request, string[] path, string? body)
    {
        if (path.Length == 4 && path[3] == "account")
            return CreateWorkerAccount(request, Id(path[2]), body);
        if (request.Method != HttpMethod.Get && !state.IsAdministrator) return Forbidden();
        if (path.Length == 2)
        {
            if (request.Method == HttpMethod.Get)
            {
                var query = Query(request.RequestUri!);
                var includeDeleted = query.TryGetValue("includeDeleted", out var raw)
                    && bool.TryParse(raw, out var value) && value;
                return Json(state.Workers.Values.Where(x => includeDeleted || !x.IsDeleted).ToArray());
            }
            if (request.Method != HttpMethod.Post) return NotFound();
            var input = Read<SaveMedicalWorkerRequest>(body);
            if (!ValidWorker(input)) return BadRequest("Имя и фамилия: 1–24 символа; должность: 3–24.");
            var id = Guid.NewGuid();
            var role = input.Role ?? MedicalWorkerRole.Other;
            var worker = new MedicalWorkerDto(id, input.FirstName.Trim(), input.LastName.Trim(),
                input.Position.Trim(), false, role);
            state.Workers[id] = worker;
            state.WorkerRoles[id] = UiMockState.RoleValue(role);
            state.AddAudit("MedicalWorkerCreated", "Создан демонстрационный медработник.", id);
            return Json(worker, HttpStatusCode.Created);
        }
        if (path.Length != 3 || !state.Workers.TryGetValue(Id(path[2]), out var current)) return NotFound();
        if (request.Method == HttpMethod.Get) return Json(current);
        if (request.Method == HttpMethod.Delete)
        {
            state.Workers[current.Id] = current with { IsDeleted = true };
            foreach (var account in state.Accounts.Values.Where(x => x.MedicalWorkerId == current.Id).ToArray())
                state.Accounts[account.Id] = account with { IsActive = false, UpdatedAt = DateTimeOffset.UtcNow };
            state.AddAudit("MedicalWorkerDeleted", "Медработник удалён, аккаунт деактивирован.", current.Id);
            return NoContent();
        }
        if (request.Method != HttpMethod.Put) return NotFound();
        if (current.IsDeleted) return Error(HttpStatusCode.Conflict, "Удалённого сотрудника нельзя редактировать.");
        var update = Read<SaveMedicalWorkerRequest>(body);
        if (!ValidWorker(update)) return BadRequest("Имя и фамилия: 1–24 символа; должность: 3–24.");
        var updatedRole = update.Role ?? current.Role ?? MedicalWorkerRole.Other;
        var updated = current with
        {
            FirstName = update.FirstName.Trim(), LastName = update.LastName.Trim(),
            Position = update.Position.Trim(), Role = updatedRole
        };
        state.Workers[current.Id] = updated;
        state.WorkerRoles[current.Id] = UiMockState.RoleValue(updatedRole);
        state.AddAudit("MedicalWorkerUpdated", "Изменён демонстрационный медработник.", current.Id);
        return Json(updated);
    }

    private HttpResponseMessage Patients(HttpRequestMessage request, string[] path, string? body)
    {
        if (state.IsAdministrator) return Forbidden();
        if (path.Length == 4 && path[3] == "medical-record") return PatientRecord(request, Id(path[2]));
        if (request.Method != HttpMethod.Get && state.CurrentMedicalRole is not ("doctor" or "nurse"))
            return Forbidden();
        if (path.Length == 2)
        {
            if (request.Method == HttpMethod.Get) return Json(state.Patients.Values.ToArray());
            if (request.Method != HttpMethod.Post) return NotFound();
            var input = Read<SavePatientRequest>(body);
            if (!ValidPatient(input)) return BadRequest("Проверьте имя, фамилию, дату и комментарий до 32 символов.");
            var id = Guid.NewGuid();
            var patient = new PatientDto(id, input.FirstName.Trim(), input.LastName.Trim(),
                input.MiddleName?.Trim(), input.BirthDate, input.Sex, input.Comment?.Trim());
            state.Patients[id] = patient;
            state.AddAudit("PatientCreated", "Создан демонстрационный пациент.", id);
            return Json(patient, HttpStatusCode.Created);
        }
        if (path.Length != 3 || !state.Patients.TryGetValue(Id(path[2]), out var current)) return NotFound();
        if (request.Method == HttpMethod.Get) return Json(current);
        if (request.Method == HttpMethod.Delete)
        {
            if (current.HasMedicalRecord)
                return Error(HttpStatusCode.Conflict, "Нельзя удалить пациента с электронной медицинской картой.");
            state.Patients.Remove(current.Id);
            state.AddAudit("PatientDeleted", "Удалён демонстрационный пациент без ЭМК.", current.Id);
            return NoContent();
        }
        if (request.Method != HttpMethod.Put) return NotFound();
        var update = Read<SavePatientRequest>(body);
        if (!ValidPatient(update)) return BadRequest("Проверьте имя, фамилию, дату и комментарий до 32 символов.");
        var updated = current with
        {
            FirstName = update.FirstName.Trim(), LastName = update.LastName.Trim(),
            MiddleName = update.MiddleName?.Trim(), BirthDate = update.BirthDate,
            Sex = update.Sex, Comment = update.Comment?.Trim()
        };
        state.Patients[current.Id] = updated;
        state.AddAudit("PatientUpdated", "Изменён демонстрационный пациент.", current.Id);
        return Json(updated);
    }

    private HttpResponseMessage Diagnoses(HttpRequestMessage request, string[] path, string? body, string? import)
    {
        if (request.Method != HttpMethod.Get && !state.IsAdministrator) return Forbidden();
        if (path.Length == 3 && path[2] == "import" && request.Method == HttpMethod.Post)
            return ImportDiagnoses(import);
        if (path.Length == 2)
        {
            if (request.Method == HttpMethod.Get) return Json(state.Diagnoses.Values.ToArray());
            if (request.Method != HttpMethod.Post) return NotFound();
            var input = Read<SaveDiagnosisRequest>(body);
            if (!ValidDiagnosis(input)) return BadRequest("Код должен содержать 6 символов, наименование — 3–24.");
            if (DuplicateDiagnosis(input, null)) return Error(HttpStatusCode.Conflict, "Код или наименование уже существует.");
            var diagnosis = new DiagnosisDefinitionDto(Guid.NewGuid(), input.Code.Trim(), input.Name.Trim());
            state.Diagnoses[diagnosis.Id] = diagnosis;
            return Json(diagnosis, HttpStatusCode.Created);
        }
        if (path.Length != 3 || !state.Diagnoses.TryGetValue(Id(path[2]), out var current)) return NotFound();
        if (request.Method == HttpMethod.Get) return Json(current);
        if (request.Method == HttpMethod.Delete)
        {
            state.Diagnoses.Remove(current.Id);
            return NoContent();
        }
        if (request.Method != HttpMethod.Put) return NotFound();
        var update = Read<SaveDiagnosisRequest>(body);
        if (!ValidDiagnosis(update)) return BadRequest("Код должен содержать 6 символов, наименование — 3–24.");
        if (DuplicateDiagnosis(update, current.Id)) return Error(HttpStatusCode.Conflict, "Код или наименование уже существует.");
        var updated = current with { Code = update.Code.Trim(), Name = update.Name.Trim() };
        state.Diagnoses[current.Id] = updated;
        return Json(updated);
    }

    private HttpResponseMessage ImportDiagnoses(string? csv)
    {
        if (csv is null) return BadRequest("Выберите один CSV-файл.");
        if (Encoding.UTF8.GetByteCount(csv) > DiagnosisImportLimits.MaxFileBytes)
            return Error(HttpStatusCode.RequestEntityTooLarge, "CSV-файл превышает 1 МиБ.");
        var rows = ParseCsv(csv.TrimStart('\uFEFF'));
        if (rows is null || rows.Count < 2 || !rows[0].SequenceEqual(["Code", "Name"]) ||
            rows.Count - 1 > DiagnosisImportLimits.MaxRows || rows.Skip(1).Any(x => x.Length != 2))
            return BadRequest("Ожидается CSV с заголовком Code,Name и корректными строками.");
        var inputs = rows.Skip(1).Select(x => new SaveDiagnosisRequest(x[0].Trim(), x[1].Trim())).ToArray();
        if (inputs.Any(x => !ValidDiagnosis(x)) || inputs.Select(x => x.Code).Distinct().Count() != inputs.Length)
            return BadRequest("CSV содержит неверные значения или повторяющиеся коды.");
        var result = state.Diagnoses.ToDictionary(x => x.Key, x => x.Value);
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        foreach (var input in inputs)
        {
            var existing = result.Values.FirstOrDefault(x => x.Code == input.Code);
            if (existing is null)
            {
                var diagnosis = new DiagnosisDefinitionDto(Guid.NewGuid(), input.Code, input.Name);
                result[diagnosis.Id] = diagnosis;
                created++;
            }
            else if (existing.Name == input.Name) unchanged++;
            else
            {
                result[existing.Id] = existing with { Name = input.Name };
                updated++;
            }
        }
        if (result.Values.Select(x => x.Name).Distinct().Count() != result.Count)
            return Error(HttpStatusCode.Conflict, "Наименования диагнозов должны быть уникальными.");
        state.Diagnoses.Clear();
        foreach (var item in result) state.Diagnoses.Add(item.Key, item.Value);
        return Json(new DiagnosisImportResultDto(created, updated, unchanged));
    }

    private bool DuplicateDiagnosis(SaveDiagnosisRequest input, Guid? exceptId) => state.Diagnoses.Values
        .Any(x => x.Id != exceptId && (x.Code == input.Code.Trim() || x.Name == input.Name.Trim()));

    private static bool ValidText(string? value, int min, int max) => value?.Trim() is { } text
        && text.Length >= min && text.Length <= max && !text.Contains('\0');
    private static bool ValidWorker(SaveMedicalWorkerRequest input) => ValidText(input.FirstName, 1, 24)
        && ValidText(input.LastName, 1, 24) && ValidText(input.Position, 3, 24);
    private static bool ValidPatient(SavePatientRequest input) => ValidText(input.FirstName, 1, 64)
        && ValidText(input.LastName, 1, 64) && (input.MiddleName is null || input.MiddleName.Length <= 64)
        && input.BirthDate != default && Enum.IsDefined(input.Sex)
        && (input.Comment is null || input.Comment.Length <= 32);
    private static bool ValidDiagnosis(SaveDiagnosisRequest input) => ValidText(input.Code, 6, 6)
        && ValidText(input.Name, 3, 24);

    private static List<string[]>? ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (!quoted && c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (!quoted && c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(field.ToString());
                if (fields.Any(x => x.Length > 0)) rows.Add(fields.ToArray());
                fields.Clear();
                field.Clear();
            }
            else field.Append(c);
        }
        if (quoted) return null;
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }
        return rows;
    }
}
