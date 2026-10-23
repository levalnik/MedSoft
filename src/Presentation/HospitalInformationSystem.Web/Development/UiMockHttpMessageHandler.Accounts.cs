using System.Net;
using System.Text.Json;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web.Development;

public sealed partial class UiMockHttpMessageHandler
{
    private HttpResponseMessage Accounts(HttpRequestMessage request, string[] path, string? body)
    {
        if (!state.IsAdministrator) return Forbidden();
        if (path.Length == 2 && request.Method == HttpMethod.Get)
            return Json(state.Accounts.Values.Select(AccountWire).ToArray());
        if (path.Length == 2 && request.Method == HttpMethod.Post)
        {
            var input = Read<CreateAccountRequest>(body);
            if (!ValidUserName(input.UserName) || !ValidPassword(input.Password)
                || input.Role is not (ApplicationRoles.Administrator or ApplicationRoles.MedicalWorker))
                return BadRequest("Проверьте логин, пароль и роль.");
            if (input.Role == ApplicationRoles.MedicalWorker && (input.MedicalWorkerId is not { } workerId
                || !state.Workers.TryGetValue(workerId, out var worker) || worker.IsDeleted))
                return BadRequest("Выберите действующего медработника.");
            if (input.Role == ApplicationRoles.Administrator && input.MedicalWorkerId is not null)
                return BadRequest("Администратор не привязывается к медработнику.");
            if (DuplicateAccount(input.UserName, input.MedicalWorkerId)) return AccountConflict();
            var account = NewAccount(input.UserName, input.Role, input.MedicalWorkerId, input.Password);
            return Json(AccountWire(account), HttpStatusCode.Created);
        }
        if (path.Length != 4 || !state.Accounts.TryGetValue(Id(path[2]), out var current)) return NotFound();
        if (path[3] == "reset-password" && request.Method == HttpMethod.Post)
        {
            if (current.Role == ApplicationRoles.Administrator)
                return Error(HttpStatusCode.Conflict, "Демонстрационный администратор не сбрасывается этим действием.");
            var password = GeneratePassword();
            state.Passwords[current.Id] = password;
            state.Accounts[current.Id] = current with { UpdatedAt = DateTimeOffset.UtcNow, IsLockedOut = false };
            state.AddAudit("AccountChanged", "Сброшен пароль демонстрационного аккаунта.", current.Id);
            return Json(new ResetAccountPasswordResultDto(current.Id, password, 0));
        }
        if (path[3] == "activity" && request.Method == HttpMethod.Put)
        {
            var input = Read<SetAccountActiveRequest>(body);
            if (current.Role == ApplicationRoles.Administrator)
                return Error(HttpStatusCode.Conflict, "Нельзя менять активность демонстрационного администратора.");
            if (input.IsActive && current.MedicalWorkerId is { } id && state.Workers[id].IsDeleted)
                return Error(HttpStatusCode.Conflict, "Удалённому медработнику нельзя активировать аккаунт.");
            var account = current with { IsActive = input.IsActive, UpdatedAt = DateTimeOffset.UtcNow };
            state.Accounts[current.Id] = account;
            state.AddAudit("AccountChanged", input.IsActive ? "Аккаунт активирован." : "Аккаунт деактивирован.", current.Id);
            return Json(new SetAccountActiveResultDto(AccountDto(account), 0));
        }
        return NotFound();
    }

    private HttpResponseMessage CreateWorkerAccount(HttpRequestMessage request, Guid workerId, string? body)
    {
        if (!state.IsAdministrator) return Forbidden();
        if (request.Method != HttpMethod.Post) return NotFound();
        if (!state.Workers.TryGetValue(workerId, out var worker)) return NotFound();
        if (worker.IsDeleted) return Error(HttpStatusCode.Conflict, "Для удалённого сотрудника нельзя создать аккаунт.");
        var input = Read<CreateWorkerAccountRequest>(body);
        if (!ValidUserName(input.UserName)) return BadRequest("Логин: 3–16 символов, латинские буквы, цифры, . _ @ + -.");
        if (DuplicateAccount(input.UserName, workerId)) return AccountConflict();
        var password = GeneratePassword();
        var account = NewAccount(input.UserName, ApplicationRoles.MedicalWorker, workerId, password);
        return Json(new CreateWorkerAccountResultDto(AccountDto(account), password), HttpStatusCode.Created);
    }

    private HttpResponseMessage Audit(HttpRequestMessage request)
    {
        if (!state.IsAdministrator) return Forbidden();
        if (request.Method != HttpMethod.Get) return NotFound();
        var query = Query(request.RequestUri!);
        var page = 1;
        var pageSize = 50;
        if (query.TryGetValue("page", out var rawPage) && !int.TryParse(rawPage, out page)
            || query.TryGetValue("pageSize", out var rawSize) && !int.TryParse(rawSize, out pageSize)
            || page < 1 || pageSize is < 1 or > 100)
            return BadRequest("Страница должна быть положительной, размер страницы — 1–100.");
        if (state.EnableLiveAudit && DateTimeOffset.UtcNow - state.LastAuditTick >= TimeSpan.FromSeconds(1))
        {
            state.LastAuditTick = DateTimeOffset.UtcNow;
            state.AddAudit("MedicalRecordChanged", "Периодическое демонстрационное событие UiMock.", UiMockState.MedicalRecordId);
        }
        var events = state.Audit.AsEnumerable();
        if (query.TryGetValue("eventType", out var eventType))
        {
            if (!Enum.TryParse<AuditEventType>(eventType, out var parsed) || !Enum.IsDefined(parsed))
                return BadRequest("Неизвестный тип события.");
            events = events.Where(x => x.EventType == eventType);
        }
        var from = Date(query, "from");
        var to = Date(query, "to");
        if (from > to) return BadRequest("Начало периода должно предшествовать концу.");
        if (from is { } start) events = events.Where(x => x.EventTime >= start);
        if (to is { } end) events = events.Where(x => x.EventTime <= end);
        var skip = (long)(page - 1) * pageSize;
        UiMockState.MockAuditEvent[] slice = skip > int.MaxValue ? [] : events.OrderByDescending(x => x.EventTime)
            .ThenByDescending(x => x.EventId).Skip((int)skip).Take(pageSize + 1).ToArray();
        var result = slice.Take(pageSize).Select(x => new AuditEventDto(x.EventId, x.EventTime,
            Enum.Parse<AuditEventType>(x.EventType), new AuditActorDto(
                x.IsSystem ? AuditActorKind.System : x.UserId.HasValue ? AuditActorKind.User : AuditActorKind.Anonymous,
                x.UserId, x.DisplayName, $"ui-mock-{x.EventId:N}", x.IsSystem ? "UiMock" : null), x.Message,
            new Dictionary<string, JsonElement>
            {
                ["reason"] = JsonSerializer.SerializeToElement("UiMock"),
                ["targetId"] = JsonSerializer.SerializeToElement(x.TargetId)
            })).ToArray();
        return Json(new PagedResult<AuditEventDto>(result, page, pageSize, slice.Length > pageSize));
    }

    private UiMockState.MockAccount NewAccount(string userName, string role, Guid? workerId, string password)
    {
        var now = DateTimeOffset.UtcNow;
        var account = new UiMockState.MockAccount(Guid.NewGuid(), userName.Trim(), role, workerId, true, false, now, now);
        state.Accounts[account.Id] = account;
        state.Passwords[account.Id] = password;
        state.AddAudit("AccountCreated", "Создан демонстрационный аккаунт.", account.Id);
        return account;
    }

    private Lab2AccountDto AccountDto(UiMockState.MockAccount account)
    {
        var worker = account.MedicalWorkerId is { } id ? state.Workers.GetValueOrDefault(id) : null;
        return new Lab2AccountDto(account.Id, account.UserName, account.Role, account.MedicalWorkerId,
            worker is null ? null : $"{worker.LastName} {worker.FirstName}",
            account.MedicalWorkerId is { } workerId ? UiMockState.ToRole(state.WorkerRoles[workerId]) : null,
            account.IsActive, account.IsLockedOut, account.CreatedAt, account.UpdatedAt);
    }

    private object AccountWire(UiMockState.MockAccount account)
    {
        var dto = AccountDto(account);
        return new
        {
            dto.Id, dto.UserName, Role = dto.AccountRole, dto.AccountRole, dto.MedicalWorkerId,
            dto.MedicalWorkerName, dto.MedicalWorkerRole, dto.IsActive, dto.IsLockedOut, dto.CreatedAt, dto.UpdatedAt
        };
    }

    private bool DuplicateAccount(string userName, Guid? workerId) => state.Accounts.Values.Any(x =>
        string.Equals(x.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase)
        || workerId.HasValue && x.MedicalWorkerId == workerId);
    private static HttpResponseMessage AccountConflict() => Error(HttpStatusCode.Conflict,
        "Логин уже используется или для медработника уже создан аккаунт.");
    private static string GeneratePassword() => $"Dd1{Guid.NewGuid():N}"[..12];
    private static bool ValidUserName(string? value) => value?.Trim() is { Length: >= 3 and <= 16 } text
        && text.All(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '_' or '@' or '+' or '-');
}
