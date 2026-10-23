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
using HospitalInformationSystem.Web.Gateways;
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
    public async Task Medical_record_author_list_contains_only_the_signed_in_worker()
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
    public async Task Refreshing_edit_choices_clears_deleted_author_and_diagnosis()
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
    public async Task Changing_the_selected_author_cannot_submit_a_different_workers_identity()
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
