using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Pages.Admin;
using HospitalInformationSystem.Web.Pages.Medical;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class WebPageRegressionTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("{\"text\":42}")]
    [InlineData("{\"text\":{}}")]
    public void ReadHistory_UnexpectedPayloadShape_ShouldKeepPageUsable(string payload)
    {
        var item = new MedicalRecordHistoryEventDto(1, DateTimeOffset.UtcNow, Guid.NewGuid(), "Доктор", "Врач",
            "PrescriptionAdded", null, payload);
        var describe = typeof(MedicalRecord).GetMethod("DescribeEvent", BindingFlags.Static | BindingFlags.NonPublic)!;

        var action = () => describe.Invoke(null, [item]);

        action.Should().NotThrow();
    }

    [Fact]
    public async Task RefreshPatients_FailedRefreshAfterDeletion_ShouldKeepPatientDeleted()
    {
        var patient = new PatientDto(Guid.NewGuid(), "Иван", "Иванов", null, new DateOnly(2000, 1, 1),
            AdministrativeSex.Male, null);
        using var http = CreateClient((request, _) => Task.FromResult(request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var page = new Patients();
        Inject(page, "Api", new PatientsApiClient(http));
        Set(page, "_cts", new CancellationTokenSource());
        Set(page, "_patients", new[] { patient });
        Set(page, "_pendingDelete", patient);

        await InvokeAsync(page, "DeleteAsync");

        Get<IReadOnlyList<PatientDto>>(page, "_patients").Should().BeEmpty();
        Get<bool>(page, "_deleting").Should().BeFalse();
        Get<string>(page, "_error").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshDiagnoses_FailedRefreshAfterCreation_ShouldKeepCreatedDiagnosis()
    {
        var diagnosis = new DiagnosisDefinitionDto(Guid.NewGuid(), "A00.00", "Холера");
        using var http = CreateClient((request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? Json(diagnosis)
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var page = new Diagnoses();
        Inject(page, "Api", new DiagnosesApiClient(http));
        Set(page, "_cts", new CancellationTokenSource());
        Invoke(page, "BeginCreate");
        var form = Get<object>(page, "_form");
        form.GetType().GetProperty("Code")!.SetValue(form, diagnosis.Code);
        form.GetType().GetProperty("Name")!.SetValue(form, diagnosis.Name);

        await InvokeAsync(page, "SaveAsync");

        Get<IReadOnlyList<DiagnosisDefinitionDto>>(page, "_diagnoses").Should().ContainSingle().Which.Should().Be(diagnosis);
        Get<bool>(page, "_formOpen").Should().BeFalse();
        Get<string>(page, "_listError").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImportDiagnoses_ImportInProgress_ShouldPreventOverlappingEditsAndDeletes()
    {
        var diagnosis = new DiagnosisDefinitionDto(Guid.NewGuid(), "A00.00", "Холера");
        using var http = CreateClient((_, _) => throw new InvalidOperationException("No write is allowed during import."));
        using var page = new Diagnoses();
        Inject(page, "Api", new DiagnosesApiClient(http));
        Set(page, "_cts", new CancellationTokenSource());
        Invoke(page, "BeginEdit", diagnosis);
        var originalForm = Get<object>(page, "_form");
        Set(page, "_pendingDelete", diagnosis);
        Set(page, "_importing", true);

        Invoke(page, "BeginCreate");
        Invoke(page, "BeginEdit", diagnosis with { Name = "Изменённое название" });
        await InvokeAsync(page, "SaveAsync");
        await InvokeAsync(page, "DeleteAsync");

        Get<object>(page, "_form").Should().BeSameAs(originalForm);
        Get<Guid?>(page, "_editingId").Should().Be(diagnosis.Id);
    }

    [Fact]
    public async Task LoadAuthors_SignedInMedicalWorker_ShouldReturnOnlyCurrentWorker()
    {
        var current = new MedicalWorkerDto(Guid.NewGuid(), "Иван", "Иванов", "Терапевт", false);
        var other = new MedicalWorkerDto(Guid.NewGuid(), "Пётр", "Петров", "Хирург", false);
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, current.Id));
        using var http = CreateClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.Contains("medical-workers")
            ? Json(new[] { current, other })
            : Json(Array.Empty<DiagnosisDefinitionDto>())));
        using var page = new MedicalRecord();
        Inject(page, "WorkersApi", new MedicalWorkersApiClient(http));
        Inject(page, "DiagnosesApi", new DiagnosesApiClient(http));
        Inject(page, "Session", state);
        Set(page, "_cts", new CancellationTokenSource());

        await InvokeAsync(page, "BeginEditAsync");

        Get<IReadOnlyList<MedicalWorkerDto>>(page, "_workers").Should().Equal(current);
        Get<Guid?>(page, "_authorId").Should().BeNull();
        Get<bool>(page, "_editDataLoading").Should().BeFalse();
    }

    [Fact]
    public async Task RefreshEditChoices_DeletedAuthorAndDiagnosis_ShouldClearSelections()
    {
        using var http = CreateClient((_, _) => Task.FromResult(Json(Array.Empty<object>())));
        using var page = new MedicalRecord();
        Inject(page, "WorkersApi", new MedicalWorkersApiClient(http));
        Inject(page, "DiagnosesApi", new DiagnosesApiClient(http));
        Inject(page, "Session", new SessionState());
        using var cancellation = new CancellationTokenSource();
        Set(page, "_authorId", Guid.NewGuid());
        Set(page, "_selectedDiagnosisId", Guid.NewGuid());

        await InvokeAsync(page, "RefreshEditDataAsync", cancellation.Token);

        Get<Guid?>(page, "_authorId").Should().BeNull();
        Get<Guid?>(page, "_selectedDiagnosisId").Should().BeNull();
    }

    [Fact]
    public async Task SaveRecord_ChangedAuthorSelection_ShouldPreventIdentityForgery()
    {
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid()));
        using var http = CreateClient((_, _) => throw new InvalidOperationException("Another author must not be sent."));
        using var page = new MedicalRecord();
        Inject(page, "RecordsApi", new MedicalRecordsApiClient(http));
        Inject(page, "Session", state);
        Set(page, "_cts", new CancellationTokenSource());
        Set(page, "_authorId", Guid.NewGuid());

        await InvokeAsync(page, "CreateRecordAsync");

        Get<bool>(page, "_busy").Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAccounts_OlderResponse_ShouldPreserveNewlyCreatedAccount()
    {
        var staleResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var administrator = new AccountDto(Guid.NewGuid(), "admin", ApplicationRoles.Administrator, null, false);
        var created = new AccountDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid(), false);
        var accountRequests = 0;
        using var http = CreateClient((request, _) => request.RequestUri!.AbsolutePath == "/api/accounts"
            ? ++accountRequests == 1 ? staleResponse.Task : Task.FromResult(Json(new[] { administrator, created }))
            : Task.FromResult(Json(Array.Empty<MedicalWorkerDto>())));
        using var page = new Accounts();
        Inject(page, "Api", new AccountsApiClient(http));
        Inject(page, "WorkersApi", new MedicalWorkersApiClient(http));

        var olderLoad = InvokeAsync(page, "LoadAsync");
        await InvokeAsync(page, "LoadAsync");
        staleResponse.SetResult(Json(new[] { administrator }));
        await olderLoad;

        Get<IReadOnlyList<AccountDto>>(page, "_accounts").Should().Equal(administrator, created);
        Get<bool>(page, "_loading").Should().BeFalse();
    }

    [Fact]
    public async Task CreateAccount_ListRefreshInProgress_ShouldBlockCreation()
    {
        using var http = CreateClient((_, _) => throw new InvalidOperationException("Creation during refresh is blocked."));
        using var page = new Accounts();
        Inject(page, "Api", new AccountsApiClient(http));
        Set(page, "_loading", true);

        await InvokeAsync(page, "CreateAsync");

        Get<bool>(page, "_saving").Should().BeFalse();
    }

    [Theory]
    [InlineData(ApplicationRoles.Administrator, false, false, null, "Активен")]
    [InlineData(ApplicationRoles.Administrator, true, false, null, "Временно заблокирован")]
    [InlineData(ApplicationRoles.MedicalWorker, false, true, false, "Активен")]
    [InlineData(ApplicationRoles.MedicalWorker, false, true, true, "Сотрудник удалён")]
    [InlineData(ApplicationRoles.MedicalWorker, true, true, true, "Временно заблокирован")]
    [InlineData(ApplicationRoles.MedicalWorker, false, true, null, "Нет действующего сотрудника")]
    [InlineData(ApplicationRoles.MedicalWorker, false, false, false, "Нет действующего сотрудника")]
    [InlineData(ApplicationRoles.MedicalWorker, true, false, null, "Временно заблокирован")]
    public void GetAccountStatus_LockoutAndWorkerAvailability_ShouldReflectEffectiveAccess(
        string role, bool isLockedOut, bool hasWorkerLink, bool? workerDeleted, string expected)
    {
        var workerId = Guid.NewGuid();
        var account = new AccountDto(Guid.NewGuid(), "account", role, hasWorkerLink ? workerId : null, isLockedOut);
        using var page = new Accounts();
        Set(page, "_workers", workerDeleted is { } isDeleted
            ? new[] { new MedicalWorkerDto(workerId, "Иван", "Иванов", "Врач", isDeleted) }
            : Array.Empty<MedicalWorkerDto>());

        var status = Invoke(page, "AccountStatus", account);

        status.Should().Be(expected);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new ResponseHandler(send)) { BaseAddress = new Uri("http://localhost/") };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static void Set(object page, string field, object? value) =>
        page.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, value);
    private static T Get<T>(object page, string field) =>
        (T)page.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
    private static void Inject(object page, string property, object value) =>
        page.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, value);
    private static object? Invoke(object page, string method, params object[] arguments) =>
        page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, arguments);
    private static Task InvokeAsync(object page, string method, params object[] arguments) =>
        (Task)Invoke(page, method, arguments)!;

    private sealed class ResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
