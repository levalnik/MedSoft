using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.Web.Development;

public sealed partial class UiMockHttpMessageHandler(UiMockState state) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.ResponseDelay > TimeSpan.Zero)
        {
            await Task.Delay(state.ResponseDelay, cancellationToken);
        }

        var body = request.Content is null or MultipartFormDataContent ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        string? import = null;
        if (request.Content is MultipartFormDataContent multipart)
        {
            var files = multipart.Where(x => x.Headers.ContentDisposition?.Name?.Trim('"') == "file").ToArray();
            if (files.Length == 1)
            {
                import = await files[0].ReadAsStringAsync(cancellationToken);
            }
        }

        lock (state.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.RequestCount++;
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (path.Length == 3 && path[0] == "api" && path[1] == "auth" && path[2] == "csrf")
            {
                return Json(new AntiforgeryTokenDto("ui-mock-antiforgery-token"));
            }

            if (state.NextStatusCode is { } status)
            {
                state.NextStatusCode = null;
                if (status == HttpStatusCode.Unauthorized) state.SignOut();
                return Error(status, $"Демонстрационная ошибка UiMock: {(int)status}.");
            }

            try
            {
                if (path.Length >= 3 && path[0] == "api" && path[1] == "auth")
                    return Authentication(request, path, body);
                if (!state.HasActiveSession)
                    return Error(HttpStatusCode.Unauthorized, "Войдите в демонстрационную учётную запись.");
                if (path.Length < 2 || path[0] != "api")
                    return Error(HttpStatusCode.NotFound, "Демонстрационный маршрут не найден.");
                return path[1] switch
                {
                    "medical-workers" => MedicalWorkers(request, path, body),
                    "diagnoses" => Diagnoses(request, path, body, import),
                    "patients" => Patients(request, path, body),
                    "medical-records" => MedicalRecords(request, path, body),
                    "accounts" => Accounts(request, path, body),
                    "audit" => Audit(request),
                    _ => Error(HttpStatusCode.NotImplemented, "Этот маршрут ещё не описан в UiMock.")
                };
            }
            catch (JsonException)
            {
                return Error(HttpStatusCode.BadRequest, "Некорректный формат запроса.");
            }
            catch (FormatException)
            {
                return Error(HttpStatusCode.BadRequest, "Некорректный идентификатор или дата.");
            }
        }
    }

    private HttpResponseMessage Authentication(HttpRequestMessage request, string[] path, string? body)
    {
        if (path.Length == 4 && path[2] == "setup") return Setup(request, path[3], body);
        if (path.Length != 3) return NotFound();
        if (path[2] == "session" && request.Method == HttpMethod.Get)
            return state.HasActiveSession ? Json(state.CurrentSession!) : Unauthorized();
        if (path[2] == "logout" && request.Method == HttpMethod.Post)
        {
            if (!state.HasActiveSession) return Unauthorized();
            state.AddAudit("UserLogout", "Выход из демонстрационной сессии.");
            state.SignOut();
            return NoContent();
        }
        if (path[2] == "login" && request.Method == HttpMethod.Post)
        {
            if (state.SetupRequired)
                return Error(HttpStatusCode.Conflict, "Сначала завершите демонстрационную настройку.");
            var input = Read<LoginRequest>(body);
            if (input.Portal is { } portal && !Enum.IsDefined(portal)) return BadRequest("Неизвестный экран входа.");
            var account = state.Accounts.Values.FirstOrDefault(x =>
                string.Equals(x.UserName, input.UserName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (account is null || !account.IsActive || account.IsLockedOut
                || input.Password != state.Passwords.GetValueOrDefault(account.Id)
                || account.MedicalWorkerId is { } workerId && state.Workers[workerId].IsDeleted)
            {
                state.AddAudit("LoginFailed", "Неуспешный вход в демонстрационную учётную запись.");
                return Unauthorized();
            }
            if (input.TotpCode is not null && input.TotpCode != UiMockState.DemonstrationTotpCode)
                return Unauthorized();
            if (input.Portal == LoginPortal.Administrator && account.Role != ApplicationRoles.Administrator
                || input.Portal == LoginPortal.MedicalWorker && account.Role != ApplicationRoles.MedicalWorker)
                return Unauthorized();
            state.SignIn(account);
            state.AddAudit(state.IsAdministrator ? "AdministratorLogin" : "UserLogin",
                "Вход в демонстрационную сессию UiMock.");
            return Json(state.CurrentSession!);
        }
        return NotFound();
    }

    private HttpResponseMessage Setup(HttpRequestMessage request, string operation, string? body)
    {
        if (operation == "status" && request.Method == HttpMethod.Get)
            return Json(new SetupStatusDto(state.SetupRequired));
        if (request.Method != HttpMethod.Post) return NotFound();
        if (!state.SetupRequired) return Error(HttpStatusCode.Conflict, "Настройка уже завершена.");
        if (!request.Headers.TryGetValues("X-Setup-Token", out var values)
            || values.ToArray() is not [UiMockState.SetupToken])
            return Error(HttpStatusCode.Forbidden, "Для UiMock используйте демонстрационный setup-токен.");
        if (operation == "begin")
        {
            var input = Read<BeginSetupRequest>(body);
            if (!ValidPassword(input.Password) || input.Password != input.ConfirmPassword)
                return BadRequest("Пароли должны совпадать и соответствовать требованиям 12–128 символов.");
            var challenge = Guid.NewGuid();
            var expires = DateTimeOffset.UtcNow.AddMinutes(5);
            state.SetupChallengeId = challenge;
            state.SetupChallengeExpiresAt = expires;
            state.SetupPassword = input.Password;
            return Json(new SetupChallengeDto(challenge, "JBSWY3DPEHPK3PXP",
                "otpauth://totp/UiMock:admin.demo?secret=JBSWY3DPEHPK3PXP&issuer=UiMock", expires));
        }
        if (operation == "confirm")
        {
            var input = Read<ConfirmSetupRequest>(body);
            if (state.SetupChallengeId is not { } challengeId || input.ChallengeId != challengeId
                || state.SetupChallengeExpiresAt is not { } expiresAt || expiresAt < DateTimeOffset.UtcNow
                || input.Code != UiMockState.DemonstrationTotpCode)
                return BadRequest("Для учебного UiMock введите код 012345 действующего challenge.");
            state.SetupRequired = false;
            var admin = state.Accounts.Values.Single(x => x.UserName == "admin.demo");
            state.Passwords[admin.Id] = state.SetupPassword!;
            state.SetupChallengeId = null;
            state.SetupChallengeExpiresAt = null;
            state.SetupPassword = null;
            state.AddAudit("AdministratorSetup", "Завершён демонстрационный мастер, криптография не выполнялась.");
            state.SignOut();
            return NoContent();
        }
        return NotFound();
    }

    private static bool ValidPassword(string? value) => value is { Length: >= 12 and <= 128 }
        && value.Any(char.IsAsciiLetterUpper) && value.Any(char.IsAsciiLetterLower)
        && value.Any(char.IsAsciiDigit) && value.Any(c => !char.IsLetterOrDigit(c));

    private static T Read<T>(string? body) => JsonSerializer.Deserialize<T>(body ?? "null", JsonOptions)
        ?? throw new JsonException();

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = JsonContent.Create(value, options: JsonOptions),
        Headers = { CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true } }
    };

    private static HttpResponseMessage Error(HttpStatusCode status, string message) => Json(new
        { title = message, detail = message, status = (int)status, traceId = "ui-mock-only" }, status);
    private static HttpResponseMessage BadRequest(string message) => Error(HttpStatusCode.BadRequest, message);
    private static HttpResponseMessage Forbidden() => Error(HttpStatusCode.Forbidden, "Недостаточно прав для этой демонстрационной роли.");
    private static HttpResponseMessage Unauthorized() => Error(HttpStatusCode.Unauthorized, "Недействительная демонстрационная сессия.");
    private static HttpResponseMessage NotFound() => Error(HttpStatusCode.NotFound, "Демонстрационная запись не найдена.");
    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);
    private static Guid Id(string value) => Guid.Parse(value);

    private static Dictionary<string, string> Query(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "";
            if (!result.TryAdd(key, value)) throw new FormatException("Параметр запроса указан несколько раз.");
        }
        return result;
    }

    private static DateTimeOffset? Date(Dictionary<string, string> query, string key)
    {
        if (!query.TryGetValue(key, out var value)) return null;
        if (!OffsetTimestamp().IsMatch(value) || !DateTimeOffset.TryParse(value,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            throw new FormatException("Укажите время ISO 8601 с Z или смещением.");
        return instant;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTimestamp();
}
