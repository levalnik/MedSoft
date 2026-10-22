using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Gateways;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class Lab2ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor, "doctor")]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician, "laboratory-technician")]
    [InlineData(MedicalWorkerRole.Nurse, "nurse")]
    [InlineData(MedicalWorkerRole.Other, "other")]
    public void Medical_role_has_explicit_stable_wire_value(MedicalWorkerRole role, string wireValue)
    {
        var json = JsonSerializer.Serialize(role, JsonOptions);

        json.Should().Be($"\"{wireValue}\"");
        JsonSerializer.Deserialize<MedicalWorkerRole>(json, JsonOptions).Should().Be(role);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("\"0\"")]
    [InlineData("\"Doctor\"")]
    [InlineData("\"laboratoryTechnician\"")]
    [InlineData("\"Administrator\"")]
    public void Numeric_unknown_or_ambiguous_medical_roles_are_rejected(string json)
    {
        var action = () => JsonSerializer.Deserialize<MedicalWorkerRole>(json, JsonOptions);

        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void Old_worker_and_session_wire_contracts_remain_readable_without_new_roles()
    {
        var id = Guid.NewGuid();
        var worker = JsonSerializer.Deserialize<MedicalWorkerDto>($$"""
            {"id":"{{id}}","firstName":"Иван","lastName":"Орлов","position":"Врач","isDeleted":false}
            """, JsonOptions);
        var session = JsonSerializer.Deserialize<SessionDto>($$"""
            {"id":"{{id}}","userName":"doctor","role":"MedicalWorker","medicalWorkerId":"{{id}}"}
            """, JsonOptions);

        worker!.Role.Should().BeNull();
        session!.MedicalWorkerRole.Should().BeNull();
        new SaveMedicalWorkerRequest("Иван", "Орлов", "Врач").Role.Should().BeNull();
    }

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor, true, true)]
    [InlineData(MedicalWorkerRole.Nurse, true, false)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician, false, false)]
    [InlineData(MedicalWorkerRole.Other, false, false)]
    public void Shared_UI_permissions_follow_the_lab2_matrix(MedicalWorkerRole role, bool canCreate, bool canEdit)
    {
        var session = new AuthenticationSessionDto(Guid.NewGuid(), "demo", ApplicationRoles.MedicalWorker,
            Guid.NewGuid(), role);

        Lab2SessionPermissions.CanReadClinicalData(session).Should().BeTrue();
        Lab2SessionPermissions.CanManagePatients(session).Should().Be(canCreate);
        Lab2SessionPermissions.CanCreateMedicalRecord(session).Should().Be(canCreate);
        Lab2SessionPermissions.CanEditMedicalRecord(session).Should().Be(canEdit);
        Lab2SessionPermissions.CanManageAdministration(session).Should().BeFalse();
    }

    [Fact]
    public void Unknown_role_and_administrator_do_not_gain_clinical_permissions()
    {
        var unknown = new AuthenticationSessionDto(Guid.NewGuid(), "legacy", ApplicationRoles.MedicalWorker,
            Guid.NewGuid(), null);
        var admin = unknown with { AccountRole = ApplicationRoles.Administrator, MedicalWorkerRole = MedicalWorkerRole.Doctor };

        Lab2SessionPermissions.CanReadClinicalData(unknown).Should().BeFalse();
        Lab2SessionPermissions.CanCreateMedicalRecord(unknown).Should().BeFalse();
        Lab2SessionPermissions.CanReadClinicalData(admin).Should().BeFalse();
        Lab2SessionPermissions.CanEditMedicalRecord(admin).Should().BeFalse();
        Lab2SessionPermissions.CanManageAdministration(admin).Should().BeTrue();
        Lab2SessionPermissions.CanReadClinicalData(null).Should().BeFalse();
    }

    [Fact]
    public void Audit_actor_and_event_types_serialize_without_numbers()
    {
        JsonSerializer.Serialize(AuditActorKind.Anonymous, JsonOptions).Should().Be("\"anonymous\"");
        JsonSerializer.Serialize(AuditEventType.AccountChanged, JsonOptions).Should().Be("\"AccountChanged\"");
        var actorAction = () => JsonSerializer.Deserialize<AuditActorKind>("0", JsonOptions);
        var eventAction = () => JsonSerializer.Deserialize<AuditEventType>("0", JsonOptions);
        actorAction.Should().Throw<JsonException>();
        eventAction.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task Unsupported_setup_is_an_error_instead_of_fake_success(HttpStatusCode status)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var gateway = CreateAuthenticationGateway(http, new SessionState());

        var action = () => gateway.GetSetupStatusAsync();

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task Setup_credential_is_sent_only_in_header()
    {
        string? setupHeader = null;
        string? body = null;
        using var http = CreateHttp(async (request, cancellationToken) =>
        {
            setupHeader = request.Headers.GetValues("X-Setup-Token").Single();
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SetupChallengeDto(Guid.NewGuid(), "DEMO-SECRET",
                    "otpauth://totp/demo", DateTimeOffset.UtcNow.AddMinutes(5)))
            };
        });

        await CreateAuthenticationGateway(http, new SessionState()).BeginSetupAsync(
            new BeginSetupRequest("SyntheticPassword1!", "SyntheticPassword1!"), "synthetic-setup-token");

        setupHeader.Should().Be("synthetic-setup-token");
        body.Should().NotContain("synthetic-setup-token");
    }

    [Fact]
    public async Task Legacy_authentication_adapter_preserves_known_role_without_inventing_one()
    {
        var id = Guid.NewGuid();
        using var http = CreateHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SessionDto(Guid.NewGuid(), "legacy", ApplicationRoles.MedicalWorker, id))
        }));
        var state = new SessionState();

        var result = await CreateAuthenticationGateway(http, state).GetSessionAsync();

        result!.AccountRole.Should().Be(ApplicationRoles.MedicalWorker);
        result.MedicalWorkerId.Should().Be(id);
        result.MedicalWorkerRole.Should().BeNull();
        state.Current.Should().NotBeNull();
    }

    [Fact]
    public async Task Medical_adapter_derives_author_from_current_session_on_every_change()
    {
        var authors = new List<string>();
        using var http = CreateHttp((request, _) =>
        {
            authors.Add(request.Headers.GetValues("X-Medical-Worker-Id").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var state = new SessionState();
        var firstAuthor = Guid.NewGuid();
        var secondAuthor = Guid.NewGuid();
        var gateway = new MedicalRecordsGateway(new MedicalRecordsApiClient(http), state);
        state.SetSession(new SessionDto(Guid.NewGuid(), "first", ApplicationRoles.MedicalWorker, firstAuthor));
        await gateway.AddPrescriptionAsync(Guid.NewGuid(), "Учебное назначение");
        state.SetSession(new SessionDto(Guid.NewGuid(), "second", ApplicationRoles.MedicalWorker, secondAuthor));
        await gateway.AddPrescriptionAsync(Guid.NewGuid(), "Другое назначение");

        authors.Should().Equal(firstAuthor.ToString(), secondAuthor.ToString());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Medical_adapter_rejects_missing_or_nonmedical_session_before_sending(bool administrator,
        HttpStatusCode expectedStatus)
    {
        var sends = 0;
        using var http = CreateHttp((_, _) =>
        {
            sends++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var state = new SessionState();
        if (administrator)
            state.SetSession(new SessionDto(Guid.NewGuid(), "admin", ApplicationRoles.Administrator, null));
        var gateway = new MedicalRecordsGateway(new MedicalRecordsApiClient(http), state);

        var action = () => gateway.AddPrescriptionAsync(Guid.NewGuid(), "Учебное назначение");

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(expectedStatus);
        sends.Should().Be(0);
    }

    [Fact]
    public async Task Audit_dates_are_encoded_as_UTC_and_filters_are_preserved()
    {
        Uri? requestedUri = null;
        using var http = CreateHttp((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PagedResult<AuditEventDto>([], 2, 10, false))
            });
        });
        var from = new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.FromHours(3));

        await new AuditGateway(http).ListAsync(new AuditQuery(2, 10, AuditEventType.AccountChanged, from));

        var query = Uri.UnescapeDataString(requestedUri!.Query);
        query.Should().Contain("page=2").And.Contain("pageSize=10").And.Contain("eventType=AccountChanged")
            .And.Contain("from=2026-10-22T12:00:00.0000000+00:00");
    }

    private static AuthenticationGateway CreateAuthenticationGateway(HttpClient http, SessionState state) =>
        new(http, new SessionApiClient(http, state, new AntiforgeryTokenStore()), state);

    private static HttpClient CreateHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new DelegateHandler(send)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
