using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Development;
using HospitalInformationSystem.Web.Gateways;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class UiMockTransportTests
{
    [Theory]
    [InlineData(UiMockPersona.Administrator, ApplicationRoles.Administrator, null)]
    [InlineData(UiMockPersona.Doctor, ApplicationRoles.MedicalWorker, MedicalWorkerRole.Doctor)]
    [InlineData(UiMockPersona.Nurse, ApplicationRoles.MedicalWorker, MedicalWorkerRole.Nurse)]
    [InlineData(UiMockPersona.LaboratoryTechnician, ApplicationRoles.MedicalWorker, MedicalWorkerRole.LaboratoryTechnician)]
    [InlineData(UiMockPersona.Other, ApplicationRoles.MedicalWorker, MedicalWorkerRole.Other)]
    public async Task Persona_updates_the_same_session_used_by_the_application(UiMockPersona persona,
        string accountRole, MedicalWorkerRole? medicalRole)
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(persona);

        var result = await fixture.Auth.GetSessionAsync();

        result!.AccountRole.Should().Be(accountRole);
        result.MedicalWorkerRole.Should().Be(medicalRole);
        fixture.Session.Current!.Id.Should().Be(result.Id);
        fixture.State.Reset();
        fixture.Session.Current.Should().BeNull();
        (await fixture.Auth.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Existing_catalog_client_is_stateful_and_accepts_short_names_for_lab2()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        var client = new MedicalWorkersApiClient(fixture.Http);

        var worker = await client.SaveAsync(null,
            new SaveMedicalWorkerRequest("Я", "Ли", "Врач", MedicalWorkerRole.Doctor), CancellationToken.None);
        var updated = await client.SaveAsync(worker.Id,
            new SaveMedicalWorkerRequest("Я", "Ли", "Хирург", MedicalWorkerRole.Nurse), CancellationToken.None);

        updated.Id.Should().Be(worker.Id);
        updated.Role.Should().Be(MedicalWorkerRole.Nurse);
        await client.DeleteAsync(worker.Id, CancellationToken.None);
        (await client.ListAsync(false, CancellationToken.None)).Should().NotContain(x => x.Id == worker.Id);
        (await client.ListAsync(true, CancellationToken.None)).Should().Contain(x => x.Id == worker.Id && x.IsDeleted);
    }

    [Theory]
    [InlineData(UiMockPersona.Nurse, true)]
    [InlineData(UiMockPersona.LaboratoryTechnician, false)]
    [InlineData(UiMockPersona.Other, false)]
    public async Task Read_only_roles_can_browse_but_only_nurse_and_doctor_can_create_patients(
        UiMockPersona persona, bool canCreate)
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(persona);
        var client = new PatientsApiClient(fixture.Http);
        (await client.ListAsync(CancellationToken.None)).Should().NotBeEmpty();
        var action = () => client.CreateAsync(new SavePatientRequest("Иван", "Учебный", null,
            new DateOnly(2000, 1, 1), AdministrativeSex.Male, null), CancellationToken.None);

        if (canCreate) (await action()).HasMedicalRecord.Should().BeFalse();
        else (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nurse_can_create_a_record_but_cannot_change_medical_contents()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Nurse);
        var records = new MedicalRecordsApiClient(fixture.Http);
        var record = await records.CreateAsync(UiMockState.PatientWithoutRecordId, UiMockState.NurseId,
            CancellationToken.None);

        var action = () => records.AddDiagnosisAsync(record.Id, UiMockState.DiagnosisId, UiMockState.NurseId,
            CancellationToken.None);

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var delete = () => new PatientsApiClient(fixture.Http).DeleteAsync(UiMockState.PatientWithoutRecordId,
            CancellationToken.None);
        (await delete.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Prescription_replacement_preserves_history_and_historical_state()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Doctor);
        var records = new MedicalRecordsApiClient(fixture.Http);
        await records.AddDiagnosisAsync(UiMockState.MedicalRecordId, UiMockState.DiagnosisId, UiMockState.DoctorId,
            CancellationToken.None);
        await records.AddPrescriptionAsync(UiMockState.MedicalRecordId, "Контроль утром", UiMockState.DoctorId,
            CancellationToken.None);
        var before = (await records.FindForPatientAsync(UiMockState.PatientWithRecordId, CancellationToken.None))!;
        var at = (await records.GetHistoryAsync(before.Id, CancellationToken.None)).Last().OccurredAtUtc;
        await Task.Delay(10);

        await records.ReplacePrescriptionAsync(before.Id, before.Prescriptions.Single().Id, "Контроль вечером",
            UiMockState.DoctorId, CancellationToken.None);
        var current = (await records.FindForPatientAsync(before.PatientId, CancellationToken.None))!;
        var history = await records.GetHistoryAsync(before.Id, CancellationToken.None);
        var snapshot = await records.GetSnapshotAsync(before.Id, at, CancellationToken.None);

        current.Version.Should().Be(5);
        current.Prescriptions.Single().Text.Should().Be("Контроль вечером");
        current.Prescriptions.Single().ReplacesPrescriptionId.Should().Be(before.Prescriptions.Single().Id);
        history.Select(x => x.Version).Should().Equal(1, 2, 3, 4, 5);
        history[^2].OccurredAtUtc.Should().Be(history[^1].OccurredAtUtc);
        snapshot.Version.Should().Be(3);
        snapshot.Prescriptions.Single().Text.Should().Be("Контроль утром");
        snapshot.Diagnoses.Should().ContainSingle();
    }

    [Fact]
    public async Task Mock_login_does_not_accept_an_account_from_the_other_portal()
    {
        using var fixture = new Fixture();
        var action = () => fixture.Auth.LoginAsync(new LoginRequest("doctor.demo", UiMockState.DemonstrationPassword,
            Portal: LoginPortal.Administrator));

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        fixture.Session.Current.Should().BeNull();
        var session = await fixture.Auth.LoginAsync(new LoginRequest("doctor.demo", UiMockState.DemonstrationPassword,
            Portal: LoginPortal.MedicalWorker));
        session.MedicalWorkerRole.Should().Be(MedicalWorkerRole.Doctor);
    }

    [Fact]
    public async Task Failed_relogin_cannot_leave_hidden_access_after_client_session_was_cleared()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        var login = () => fixture.Auth.LoginAsync(new LoginRequest("admin.demo", "wrong-password"));

        await login.Should().ThrowAsync<ApiException>();

        fixture.Session.Current.Should().BeNull();
        (await fixture.Auth.GetSessionAsync()).Should().BeNull();
        var patients = () => new PatientsApiClient(fixture.Http).ListAsync(CancellationToken.None);
        (await patients.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Account_operations_return_one_time_passwords_and_activity_is_enforced()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        var worker = await new MedicalWorkersApiClient(fixture.Http).SaveAsync(null,
            new SaveMedicalWorkerRequest("Иван", "Учебный", "Врач", MedicalWorkerRole.Doctor), CancellationToken.None);
        var accounts = new AccountManagementGateway(fixture.Http);
        var invalid = () => accounts.CreateForWorkerAsync(worker.Id, new CreateWorkerAccountRequest(new string('a', 17)));
        (await invalid.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await accounts.CreateForWorkerAsync(worker.Id, new CreateWorkerAccountRequest("new.doctor"));
        created.GeneratedPassword.Should().HaveLength(12).And.MatchRegex("^[A-Za-z0-9]+$");
        created.GeneratedPassword.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]");
        var reset = await accounts.ResetPasswordAsync(created.Account.Id);
        reset.GeneratedPassword.Should().HaveLength(12);
        reset.GeneratedPassword.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]");
        reset.GeneratedPassword.Should().NotBe(created.GeneratedPassword);
        (await accounts.SetActiveAsync(created.Account.Id, false)).Account.IsActive.Should().BeFalse();
        await fixture.Auth.LogoutAsync();
        var login = () => fixture.Auth.LoginAsync(new LoginRequest("new.doctor", reset.GeneratedPassword));
        (await login.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        await accounts.SetActiveAsync(created.Account.Id, true);
        await fixture.Auth.LogoutAsync();
        (await fixture.Auth.LoginAsync(new LoginRequest("new.doctor", reset.GeneratedPassword)))
            .MedicalWorkerId.Should().Be(worker.Id);
        fixture.State.Reset();
        fixture.Session.Current.Should().BeNull();
    }

    [Fact]
    public async Task Setup_requires_a_live_challenge_and_finishes_without_signing_in()
    {
        using var fixture = new Fixture();
        fixture.State.SetupRequired = true;
        (await fixture.Auth.GetSetupStatusAsync()).SetupRequired.Should().BeTrue();
        var noChallenge = () => fixture.Auth.ConfirmSetupAsync(
            new ConfirmSetupRequest(Guid.Empty, UiMockState.DemonstrationTotpCode), UiMockState.SetupToken);
        (await noChallenge.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        const string password = "Different-demo-password!2026";
        var challenge = await fixture.Auth.BeginSetupAsync(new BeginSetupRequest(password, password), UiMockState.SetupToken);

        await fixture.Auth.ConfirmSetupAsync(new ConfirmSetupRequest(challenge.ChallengeId,
            UiMockState.DemonstrationTotpCode), UiMockState.SetupToken);

        (await fixture.Auth.GetSetupStatusAsync()).SetupRequired.Should().BeFalse();
        fixture.Session.Current.Should().BeNull();
        (await fixture.Auth.LoginAsync(new LoginRequest("admin.demo", password))).AccountRole
            .Should().Be(ApplicationRoles.Administrator);
    }

    [Fact]
    public async Task Csv_import_is_atomic_and_repeatable_without_using_a_database()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        var client = new DiagnosesApiClient(fixture.Http);
        await using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("Code,Name\nD00.02,Новый диагноз\nX,Ошибка"));
        var action = () => client.ImportAsync(invalid, "invalid.csv", CancellationToken.None);
        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.ListAsync(CancellationToken.None)).Should().NotContain(x => x.Code == "D00.02");
        const string csv = "Code,Name\nD00.02,\"Учебный, второй\"";
        await using var valid = new NonSeekableStream(Encoding.UTF8.GetBytes(csv));
        (await client.ImportAsync(valid, "valid.csv", CancellationToken.None)).Created.Should().Be(1);
        await using var repeated = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        (await client.ImportAsync(repeated, "valid.csv", CancellationToken.None)).Unchanged.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task One_shot_error_is_consumed_by_the_operation_not_by_csrf(HttpStatusCode status)
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        fixture.State.NextStatusCode = status;
        var action = () => new MedicalWorkersApiClient(fixture.Http).SaveAsync(null,
            new SaveMedicalWorkerRequest("Иван", "Учебный", "Врач"), CancellationToken.None);

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(status);

        fixture.State.NextStatusCode.Should().BeNull();
        fixture.State.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task Audit_filters_and_pagination_use_the_same_state_as_mutations()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        await new MedicalWorkersApiClient(fixture.Http).SaveAsync(null,
            new SaveMedicalWorkerRequest("Иван", "Учебный", "Врач"), CancellationToken.None);
        var audit = new AuditGateway(fixture.Http);

        var first = await audit.ListAsync(new AuditQuery(PageSize: 1, EventType: AuditEventType.MedicalWorkerCreated));
        var second = await audit.ListAsync(new AuditQuery(Page: 2, PageSize: 1,
            EventType: AuditEventType.MedicalWorkerCreated));

        first.Items.Should().ContainSingle();
        first.HasMore.Should().BeTrue();
        second.Items.Should().ContainSingle();
        second.Items[0].EventId.Should().NotBe(first.Items[0].EventId);
        first.Items[0].Actor.RequestId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_endpoints_terminate_in_the_mock_and_cancellation_is_respected()
    {
        using var fixture = new Fixture();
        fixture.State.SwitchPersona(UiMockPersona.Administrator);
        using var response = await fixture.Http.GetAsync("api/not-implemented");
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        fixture.State.ResponseDelay = TimeSpan.FromSeconds(10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => fixture.Http.GetAsync("api/patients", cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        fixture.State.RequestCount.Should().Be(1);
    }

    private sealed class Fixture : IDisposable
    {
        public SessionState Session { get; } = new();
        public UiMockState State { get; }
        public HttpClient Http { get; }
        public AuthenticationGateway Auth { get; }

        public Fixture()
        {
            State = new UiMockState(Session) { EnableLiveAudit = false };
            var tokens = new AntiforgeryTokenStore();
            Http = new HttpClient(new BrowserSessionHandler(Session, tokens)
            {
                InnerHandler = new UiMockHttpMessageHandler(State)
            }) { BaseAddress = new Uri("https://ui-mock.invalid/") };
            Auth = new AuthenticationGateway(Http, new SessionApiClient(Http, Session, tokens), Session);
        }

        public void Dispose() => Http.Dispose();
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
