using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Components.Accounts;
using HospitalInformationSystem.Web.Gateways;
using HospitalInformationSystem.Web.Pages;
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
    public void Unexpected_history_payload_shapes_do_not_break_the_page(string payload)
    {
        var item = new MedicalRecordHistoryEventDto(1, DateTimeOffset.UtcNow, Guid.NewGuid(), "Доктор", "Врач",
            "PrescriptionAdded", null, payload);
        var describe = typeof(MedicalRecord).GetMethod("DescribeEvent", BindingFlags.Static | BindingFlags.NonPublic)!;

        var action = () => describe.Invoke(null, [item]);

        action.Should().NotThrow();
    }

    [Fact]
    public async Task Failed_list_refresh_does_not_bring_back_a_deleted_patient()
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
    public async Task Failed_list_refresh_preserves_a_successfully_created_diagnosis()
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
    public async Task Catalog_import_prevents_overlapping_edits_and_deletes()
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
    public async Task Entering_edit_mode_loads_only_the_diagnoses_catalog()
    {
        var diagnosis = new DiagnosisDefinitionDto(Guid.NewGuid(), "A00.00", "Холера");
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid(),
            MedicalWorkerRole.Doctor));
        using var http = CreateClient((request, _) => request.RequestUri!.AbsolutePath.Contains("medical-workers")
            ? throw new InvalidOperationException("The author list must no longer be requested.")
            : Task.FromResult(Json(new[] { diagnosis })));
        using var page = new MedicalRecord();
        Inject(page, "DiagnosesApi", new DiagnosesApiClient(http));
        Inject(page, "Session", state);
        Set(page, "_cts", new CancellationTokenSource());

        await InvokeAsync(page, "BeginEditAsync");

        Get<IReadOnlyList<DiagnosisDefinitionDto>>(page, "_diagnoses").Should().Equal(diagnosis);
        Get<bool>(page, "_editDataLoading").Should().BeFalse();
    }

    [Fact]
    public async Task Refreshing_edit_choices_clears_a_deleted_diagnosis()
    {
        using var http = CreateClient((_, _) => Task.FromResult(Json(Array.Empty<DiagnosisDefinitionDto>())));
        using var page = new MedicalRecord();
        Inject(page, "DiagnosesApi", new DiagnosesApiClient(http));
        Inject(page, "Session", new SessionState());
        using var cancellation = new CancellationTokenSource();
        Set(page, "_selectedDiagnosisId", Guid.NewGuid());

        await InvokeAsync(page, "RefreshEditDataAsync", cancellation.Token);

        Get<Guid?>(page, "_selectedDiagnosisId").Should().BeNull();
    }

    [Fact]
    public async Task A_viewer_role_cannot_create_a_record()
    {
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "lab", ApplicationRoles.MedicalWorker, Guid.NewGuid(),
            MedicalWorkerRole.LaboratoryTechnician));
        using var http = CreateClient((_, _) => throw new InvalidOperationException("A viewer must not create a record."));
        using var page = new MedicalRecord();
        Inject(page, "Records", new MedicalRecordsGateway(new MedicalRecordsApiClient(http), state));
        Inject(page, "Session", state);
        Set(page, "_cts", new CancellationTokenSource());

        await InvokeAsync(page, "CreateRecordAsync");

        Get<bool>(page, "_busy").Should().BeFalse();
    }

    [Fact]
    public async Task An_older_account_list_response_cannot_overwrite_a_newer_one()
    {
        var staleResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var administrator = Lab2Account("admin", ApplicationRoles.Administrator, null, null);
        var doctor = Lab2Account("doctor.demo", ApplicationRoles.MedicalWorker, "Иванов Иван", MedicalWorkerRole.Doctor);
        var requests = 0;
        using var http = CreateClient((_, _) => ++requests == 1
            ? staleResponse.Task
            : Task.FromResult(Json(new[] { administrator, doctor })));
        using var page = new Accounts();
        Inject(page, "Gateway", new AccountManagementGateway(http));

        var olderLoad = InvokeAsync(page, "LoadAsync");
        await InvokeAsync(page, "LoadAsync");
        staleResponse.SetResult(Json(new[] { administrator }));
        await olderLoad;

        Get<IReadOnlyList<Lab2AccountDto>>(page, "_accounts").Should().Equal(administrator, doctor);
        Get<bool>(page, "_loading").Should().BeFalse();
    }

    [Fact]
    public void An_operation_result_updates_the_matching_account_without_a_reload()
    {
        var doctor = Lab2Account("doctor.demo", ApplicationRoles.MedicalWorker, "Иванов Иван", MedicalWorkerRole.Doctor);
        using var page = new Accounts();
        Set(page, "_accounts", new[] { doctor });

        Invoke(page, "OnAccountChanged", doctor with { IsActive = false });

        Get<IReadOnlyList<Lab2AccountDto>>(page, "_accounts").Single().IsActive.Should().BeFalse();
    }

    private static Lab2AccountDto Lab2Account(string userName, string accountRole, string? workerName,
        MedicalWorkerRole? role) =>
        new(Guid.NewGuid(), userName, accountRole, workerName is null ? null : Guid.NewGuid(), workerName, role,
            true, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("Я", "Ли", "Врач", true)]
    [InlineData("", "Ли", "Врач", false)]
    [InlineData("Иван", "Иванов", "Вр", false)]
    public void A_single_character_name_is_accepted_but_a_short_position_is_not(
        string firstName, string lastName, string position, bool valid)
    {
        var formType = typeof(MedicalWorkerEdit).GetNestedType("WorkerForm", BindingFlags.NonPublic)!;
        var form = Activator.CreateInstance(formType)!;
        formType.GetProperty("FirstName")!.SetValue(form, firstName);
        formType.GetProperty("LastName")!.SetValue(form, lastName);
        formType.GetProperty("Position")!.SetValue(form, position);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);

        isValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor, true)]
    [InlineData(MedicalWorkerRole.Nurse, true)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician, false)]
    [InlineData(MedicalWorkerRole.Other, false)]
    public void Patient_management_actions_follow_the_medical_role(MedicalWorkerRole role, bool canManage)
    {
        using var page = new Patients();
        Inject(page, "Session", Session(role));

        GetProperty<bool>(page, "CanManage").Should().Be(canManage);
    }

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor, true)]
    [InlineData(MedicalWorkerRole.Nurse, false)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician, false)]
    [InlineData(MedicalWorkerRole.Other, false)]
    public void Only_a_doctor_can_edit_a_record(MedicalWorkerRole role, bool canEdit)
    {
        using var page = new MedicalRecord();
        Inject(page, "Session", Session(role));

        GetProperty<bool>(page, "CanEdit").Should().Be(canEdit);
        GetProperty<bool>(page, "CanCreateRecord").Should()
            .Be(role is MedicalWorkerRole.Doctor or MedicalWorkerRole.Nurse);
    }

    [Fact]
    public async Task Resetting_a_password_shows_it_once_with_the_revoked_count()
    {
        var account = Lab2Account("doctor.demo", ApplicationRoles.MedicalWorker, "Иванов Иван", MedicalWorkerRole.Doctor);
        using var http = CreateClient((_, _) =>
            Task.FromResult(Json(new ResetAccountPasswordResultDto(account.Id, "NewPass123456", 2))));
        using var page = new AccountOperations();
        SetProperty(page, "Account", account);
        Inject(page, "Accounts", new AccountManagementGateway(http));

        await InvokeAsync(page, "ResetAsync");

        Get<string>(page, "_generatedPassword").Should().Be("NewPass123456");
        Get<string>(page, "_notice").Should().Contain("2");
    }

    [Fact]
    public async Task Deactivating_an_account_updates_it_and_reports_revoked_tokens()
    {
        var account = Lab2Account("doctor.demo", ApplicationRoles.MedicalWorker, "Иванов Иван", MedicalWorkerRole.Doctor);
        using var http = CreateClient((_, _) =>
            Task.FromResult(Json(new SetAccountActiveResultDto(account with { IsActive = false }, 3))));
        using var page = new AccountOperations();
        SetProperty(page, "Account", account);
        Inject(page, "Accounts", new AccountManagementGateway(http));

        await InvokeAsync(page, "SetActiveAsync", false);

        GetProperty<Lab2AccountDto>(page, "Account").IsActive.Should().BeFalse();
        Get<string>(page, "_notice").Should().Contain("3");
    }

    [Fact]
    public void Audit_polling_is_cancelled_on_dispose()
    {
        var page = new Audit();
        var cts = new CancellationTokenSource();
        Set(page, "_cts", cts);

        page.Dispose();

        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Audit_refresh_is_skipped_while_a_request_is_in_flight()
    {
        using var http = CreateClient((_, _) => throw new InvalidOperationException("A second request must not start."));
        using var page = new Audit();
        Inject(page, "Gateway", new AuditGateway(http));
        Set(page, "_cts", new CancellationTokenSource());
        Set(page, "_inFlight", true);

        await InvokeAsync(page, "RefreshAsync", false);

        Get<bool>(page, "_inFlight").Should().BeTrue();
    }

    [Fact]
    public async Task The_setup_wizard_stays_on_the_code_step_when_the_code_is_rejected()
    {
        var gateway = new StubAuthenticationGateway
        {
            Confirm = (_, _) => throw new ApiException("Неверный код.", HttpStatusCode.BadRequest)
        };
        using var page = new Setup();
        Inject(page, "Authentication", gateway);
        SetEnum(page, "_step", "Totp");
        Set(page, "_challengeId", Guid.NewGuid());
        Set(page, "_setupToken", "ui-mock-setup-token");
        SetNestedForm(page, "_confirmation", "Code", "012345");

        await InvokeAsync(page, "ConfirmAsync");

        GetField(page, "_step")!.ToString().Should().Be("Totp");
        Get<string>(page, "_error").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_successful_begin_moves_to_the_code_step_and_keeps_the_secret()
    {
        var gateway = new StubAuthenticationGateway
        {
            Begin = (_, _) => Task.FromResult(new SetupChallengeDto(Guid.NewGuid(), "SECRET-KEY",
                "otpauth://totp/Demo", DateTimeOffset.UtcNow.AddMinutes(5)))
        };
        using var page = new Setup();
        Inject(page, "Authentication", gateway);
        SetNestedForm(page, "_credentials", "SetupToken", "ui-mock-setup-token");
        SetNestedForm(page, "_credentials", "Password", "StrongPass12!");
        SetNestedForm(page, "_credentials", "ConfirmPassword", "StrongPass12!");

        await InvokeAsync(page, "BeginAsync");

        Get<string>(page, "_secret").Should().Be("SECRET-KEY");
        GetField(page, "_step")!.ToString().Should().Be("Totp");
    }

    private static SessionState Session(MedicalWorkerRole role)
    {
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "worker", ApplicationRoles.MedicalWorker, Guid.NewGuid(), role));
        return state;
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
    private static void SetProperty(object page, string property, object? value) =>
        page.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(page, value);
    private static T GetProperty<T>(object page, string property) =>
        (T)page.GetType().GetProperty(property,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(page)!;
    private static object? GetField(object page, string field) =>
        page.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page);
    private static void SetEnum(object page, string field, string value)
    {
        var info = page.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!;
        info.SetValue(page, Enum.Parse(info.FieldType, value));
    }
    private static void SetNestedForm(object page, string field, string property, object? value)
    {
        var form = GetField(page, field)!;
        form.GetType().GetProperty(property)!.SetValue(form, value);
    }
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

    private sealed class StubAuthenticationGateway : IAuthenticationGateway
    {
        public Func<BeginSetupRequest, string, Task<SetupChallengeDto>>? Begin { get; init; }
        public Func<ConfirmSetupRequest, string, Task>? Confirm { get; init; }

        public Task<SetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SetupStatusDto(true));

        public Task<SetupChallengeDto> BeginSetupAsync(BeginSetupRequest request, string setupToken,
            CancellationToken cancellationToken) => Begin!(request, setupToken);

        public Task ConfirmSetupAsync(ConfirmSetupRequest request, string setupToken,
            CancellationToken cancellationToken) => Confirm!(request, setupToken);

        public Task<AuthenticationSessionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthenticationSessionDto?> GetSessionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
