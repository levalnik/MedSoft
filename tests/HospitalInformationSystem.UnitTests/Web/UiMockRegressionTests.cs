using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Development;
using HospitalInformationSystem.Web.Gateways;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class UiMockRegressionTests
{
    [Fact]
    public async Task Legacy_account_creation_rejects_a_second_administrator_without_changing_the_list()
    {
        using var fixture = new Fixture(UiMockPersona.Administrator);
        var before = (await fixture.Http.GetFromJsonAsync<AccountDto[]>("api/accounts"))!;

        using var response = await fixture.Http.PostAsJsonAsync("api/accounts",
            new CreateAccountRequest("second.admin", "Integration.Password.6249!", ApplicationRoles.Administrator, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var after = (await fixture.Http.GetFromJsonAsync<AccountDto[]>("api/accounts"))!;
        after.Should().BeEquivalentTo(before);
        after.Count(account => account.Role == ApplicationRoles.Administrator).Should().Be(1);
        after.Should().OnlyContain(account => account.CreatedAtUtc != default && account.UpdatedAtUtc != default);
    }

    [Fact]
    public async Task Medical_mutations_return_the_same_status_and_json_contracts_as_the_real_api()
    {
        using var fixture = new Fixture(UiMockPersona.Doctor);
        var recordPath = $"api/medical-records/{UiMockState.MedicalRecordId}";
        using var diagnosisResponse = await fixture.Http.PostAsJsonAsync($"{recordPath}/diagnoses",
            new AddMedicalRecordDiagnosisRequest(UiMockState.DiagnosisId));
        diagnosisResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var diagnosis = (await diagnosisResponse.Content.ReadFromJsonAsync<MedicalRecordDiagnosisDto>())!;
        diagnosis.Code.Should().Be("D00.01");
        diagnosis.AddedByMedicalWorkerId.Should().Be(UiMockState.DoctorId);

        using var prescriptionResponse = await fixture.Http.PostAsJsonAsync($"{recordPath}/prescriptions",
            new SavePrescriptionRequest("Контроль утром"));
        prescriptionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var prescription = (await prescriptionResponse.Content.ReadFromJsonAsync<PrescriptionDto>())!;
        using var replacementResponse = await fixture.Http.PutAsJsonAsync(
            $"{recordPath}/prescriptions/{prescription.Id}", new SavePrescriptionRequest("Контроль вечером"));
        replacementResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var replacement = (await replacementResponse.Content.ReadFromJsonAsync<PrescriptionDto>())!;
        replacement.ReplacesPrescriptionId.Should().Be(prescription.Id);
        replacement.Text.Should().Be("Контроль вечером");
        replacement.CreatedByMedicalWorkerId.Should().Be(UiMockState.DoctorId);

        using var cancel = await fixture.Http.PostAsync($"{recordPath}/prescriptions/{replacement.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var remove = await fixture.Http.DeleteAsync($"{recordPath}/diagnoses/{diagnosis.Id}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("2026-10-23T12:34:56")]
    [InlineData("2026-10-23")]
    [InlineData("2026-10-23T12:34:56+99:00")]
    public async Task Historical_requests_reject_dates_that_the_real_api_cannot_accept(string value)
    {
        using var fixture = new Fixture(UiMockPersona.Doctor);
        using var response = await fixture.Http.GetAsync(
            $"api/medical-records/{UiMockState.MedicalRecordId}/snapshot?at={Uri.EscapeDataString(value)}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Historical_requests_resolve_equivalent_explicit_offsets_to_the_same_state()
    {
        using var fixture = new Fixture(UiMockPersona.Doctor);
        var records = new MedicalRecordsApiClient(fixture.Http);
        var at = (await records.GetHistoryAsync(UiMockState.MedicalRecordId, CancellationToken.None))[0].OccurredAtUtc;
        var utc = await records.GetSnapshotAsync(UiMockState.MedicalRecordId, at, CancellationToken.None);
        var local = await records.GetSnapshotAsync(UiMockState.MedicalRecordId,
            at.ToOffset(TimeSpan.FromHours(3)), CancellationToken.None);
        local.Should().BeEquivalentTo(utc);
    }

    [Theory]
    [InlineData("page=1&page=2")]
    [InlineData("pageSize=10&PAGESIZE=20")]
    [InlineData("from=2026-10-23T12%3A34%3A56")]
    [InlineData("from=not-a-date")]
    public async Task Malformed_audit_queries_return_an_http_error_instead_of_throwing_from_the_transport(string query)
    {
        using var fixture = new Fixture(UiMockPersona.Administrator);
        using var response = await fixture.Http.GetAsync($"api/audit?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Repeated_setup_headers_are_rejected_without_breaking_the_following_valid_request()
    {
        using var fixture = new Fixture(UiMockPersona.Anonymous);
        fixture.State.SetupRequired = true;
        const string password = "Setup-demo-password!2026";
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/setup/begin")
        {
            Content = JsonContent.Create(new BeginSetupRequest(password, password))
        };
        request.Headers.Add("X-Setup-Token", new[] { UiMockState.SetupToken, UiMockState.SetupToken });
        using var response = await fixture.Http.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var challenge = await new AuthenticationGateway(fixture.Http,
                new SessionApiClient(fixture.Http, fixture.Session, fixture.Tokens), fixture.Session)
            .BeginSetupAsync(new BeginSetupRequest(password, password), UiMockState.SetupToken);
        challenge.ChallengeId.Should().NotBeEmpty();
    }

    private sealed class Fixture : IDisposable
    {
        public SessionState Session { get; } = new();
        public AntiforgeryTokenStore Tokens { get; } = new();
        public UiMockState State { get; }
        public HttpClient Http { get; }

        public Fixture(UiMockPersona persona)
        {
            State = new UiMockState(Session) { EnableLiveAudit = false };
            State.SwitchPersona(persona);
            Http = new HttpClient(new BrowserSessionHandler(Session, Tokens)
            {
                InnerHandler = new UiMockHttpMessageHandler(State)
            }) { BaseAddress = new Uri("https://ui-mock.invalid/") };
            if (Session.Current?.MedicalWorkerId is { } id)
                Http.DefaultRequestHeaders.Add("X-Medical-Worker-Id", id.ToString());
        }

        public void Dispose() => Http.Dispose();
    }
}
