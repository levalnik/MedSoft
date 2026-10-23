using System.Threading;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Security;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Gateways;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class GatewayContractTests
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
    public void Serialize_MedicalRole_ShouldUseStableExplicitWireValue(MedicalWorkerRole role, string wireValue)
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
    public void Deserialize_NumericUnknownOrAmbiguousMedicalRole_ShouldRejectValue(string json)
    {
        var action = () => JsonSerializer.Deserialize<MedicalWorkerRole>(json, JsonOptions);

        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_LegacyWorkerAndSession_ShouldAllowMissingMedicalRoles()
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
    public void GetPermissions_MedicalRole_ShouldMatchClinicalPermissionMatrix(MedicalWorkerRole role, bool canCreate, bool canEdit)
    {
        var session = new AuthenticationSessionDto(Guid.NewGuid(), "demo", ApplicationRoles.MedicalWorker,
            Guid.NewGuid(), role);

        SessionPermissions.CanReadClinicalData(session).Should().BeTrue();
        SessionPermissions.CanManagePatients(session).Should().Be(canCreate);
        SessionPermissions.CanCreateMedicalRecord(session).Should().Be(canCreate);
        SessionPermissions.CanEditMedicalRecord(session).Should().Be(canEdit);
        SessionPermissions.CanManageAdministration(session).Should().BeFalse();
    }

    [Fact]
    public void GetPermissions_UnknownRoleOrAdministrator_ShouldDenyClinicalAccess()
    {
        var unknown = new AuthenticationSessionDto(Guid.NewGuid(), "legacy", ApplicationRoles.MedicalWorker,
            Guid.NewGuid(), null);
        var admin = unknown with { AccountRole = ApplicationRoles.Administrator, MedicalWorkerRole = MedicalWorkerRole.Doctor };

        SessionPermissions.CanReadClinicalData(unknown).Should().BeFalse();
        SessionPermissions.CanCreateMedicalRecord(unknown).Should().BeFalse();
        SessionPermissions.CanReadClinicalData(admin).Should().BeFalse();
        SessionPermissions.CanEditMedicalRecord(admin).Should().BeFalse();
        SessionPermissions.CanManageAdministration(admin).Should().BeTrue();
        SessionPermissions.CanReadClinicalData(null).Should().BeFalse();
    }

    [Fact]
    public void Serialize_AuditActorAndEventTypes_ShouldUseNamesInsteadOfNumbers()
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
    public async Task CompleteSetup_UnsupportedEndpoint_ShouldReturnError(HttpStatusCode status)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var gateway = CreateAuthenticationGateway(http, new SessionState());

        var action = () => gateway.GetSetupStatusAsync(CancellationToken.None);

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task CompleteSetup_SetupCredential_ShouldSendCredentialOnlyInHeader()
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
            new BeginSetupRequest("SyntheticPassword1!", "SyntheticPassword1!"), "synthetic-setup-token", CancellationToken.None);

        setupHeader.Should().Be("synthetic-setup-token");
        body.Should().NotContain("synthetic-setup-token");
    }

    [Fact]
    public async Task GetSession_LegacyAuthentication_ShouldPreserveKnownRole()
    {
        var id = Guid.NewGuid();
        using var http = CreateHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SessionDto(Guid.NewGuid(), "legacy", ApplicationRoles.MedicalWorker, id))
        }));
        var state = new SessionState();

        var result = await CreateAuthenticationGateway(http, state).GetSessionAsync(CancellationToken.None);

        result!.AccountRole.Should().Be(ApplicationRoles.MedicalWorker);
        result.MedicalWorkerId.Should().Be(id);
        result.MedicalWorkerRole.Should().BeNull();
        state.Current.Should().NotBeNull();
    }

    [Fact]
    public async Task MutateMedicalRecord_AuthenticatedSession_ShouldDeriveAuthorForEveryChange()
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
        await gateway.AddPrescriptionAsync(Guid.NewGuid(), "Учебное назначение", CancellationToken.None);
        state.SetSession(new SessionDto(Guid.NewGuid(), "second", ApplicationRoles.MedicalWorker, secondAuthor));
        await gateway.AddPrescriptionAsync(Guid.NewGuid(), "Другое назначение", CancellationToken.None);

        authors.Should().Equal(firstAuthor.ToString(), secondAuthor.ToString());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task MutateMedicalRecord_MissingOrNonmedicalSession_ShouldRejectBeforeSending(bool administrator,
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

        var action = () => gateway.AddPrescriptionAsync(Guid.NewGuid(), "Учебное назначение", CancellationToken.None);

        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(expectedStatus);
        sends.Should().Be(0);
    }

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor, true, true)]
    [InlineData(MedicalWorkerRole.Nurse, true, false)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician, false, false)]
    [InlineData(MedicalWorkerRole.Other, false, false)]
    [InlineData((MedicalWorkerRole)999, false, false)]
    [InlineData(null, true, true)]
    public async Task MutateMedicalRecord_KnownOrLegacyRole_ShouldRespectPermissionsAndCompatibility(
        MedicalWorkerRole? role, bool canCreate, bool canEdit)
    {
        var sends = 0;
        var patientId = Guid.NewGuid();
        using var http = CreateHttp((request, _) =>
        {
            sends++;
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/medical-record",
                StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new MedicalRecordDto(Guid.NewGuid(), patientId,
                        DateTimeOffset.UtcNow, 1, [], []))
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "worker", ApplicationRoles.MedicalWorker,
            Guid.NewGuid(), role));
        var gateway = new MedicalRecordsGateway(new MedicalRecordsApiClient(http), state);

        Func<Task> create = async () => await gateway.CreateAsync(patientId, CancellationToken.None);
        Func<Task> edit = () => gateway.AddPrescriptionAsync(Guid.NewGuid(), "Учебное назначение", CancellationToken.None);

        if (canCreate) await create.Should().NotThrowAsync();
        else (await create.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        if (canEdit) await edit.Should().NotThrowAsync();
        else (await edit.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        sends.Should().Be((canCreate ? 1 : 0) + (canEdit ? 1 : 0));
    }

    [Fact]
    public async Task ListAudit_NullEvent_ShouldRejectInvalidPage()
    {
        using var http = CreateHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"items":[null],"page":1,"pageSize":50,"hasMore":false}
                """, Encoding.UTF8, "application/json")
        }));

        var action = () => new AuditGateway(http).ListAsync(new AuditQuery(), CancellationToken.None);

        await action.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task ListAudit_DateAndFilterParameters_ShouldEncodeUtcDatesAndPreserveFilters()
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

        await new AuditGateway(http).ListAsync(new AuditQuery(2, 10, AuditEventType.AccountChanged, from), CancellationToken.None);

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
