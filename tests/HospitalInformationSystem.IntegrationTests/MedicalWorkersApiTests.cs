using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Security;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Initializers;
using HospitalInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalWorkersApiTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateBusinessClient();

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician)]
    [InlineData(MedicalWorkerRole.Nurse)]
    [InlineData(MedicalWorkerRole.Other)]
    public async Task SaveWorker_ExplicitMedicalRole_ShouldRoundTripAndSurviveLegacyUpdate(MedicalWorkerRole role)
    {
        using var created = await _client.PostAsJsonAsync("/api/medical-workers",
            new SaveMedicalWorkerRequest(" А ", " Б ", "Врач", role));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var worker = (await created.Content.ReadFromJsonAsync<MedicalWorkerDto>())!;
        worker.FirstName.Should().Be("А");
        worker.LastName.Should().Be("Б");
        worker.Role.Should().Be(role);
        (await _client.GetFromJsonAsync<MedicalWorkerDto>($"/api/medical-workers/{worker.Id}"))!
            .Role.Should().Be(role);
        (await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers"))!
            .Should().Contain(x => x.Id == worker.Id && x.Role == role);

        using var updated = await _client.PutAsJsonAsync($"/api/medical-workers/{worker.Id}",
            new SaveMedicalWorkerRequest("В", "Г", "Хирург"));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<MedicalWorkerDto>())!.Role.Should().Be(role);
        (await _client.GetFromJsonAsync<MedicalWorkerDto>($"/api/medical-workers/{worker.Id}"))!
            .Role.Should().Be(role);

        using var explicitOther = await _client.PutAsJsonAsync($"/api/medical-workers/{worker.Id}",
            new SaveMedicalWorkerRequest("В", "Г", "Хирург", MedicalWorkerRole.Other));
        explicitOther.StatusCode.Should().Be(HttpStatusCode.OK);
        (await explicitOther.Content.ReadFromJsonAsync<MedicalWorkerDto>())!.Role.Should().Be(MedicalWorkerRole.Other);
        (await _client.GetFromJsonAsync<MedicalWorkerDto>($"/api/medical-workers/{worker.Id}"))!
            .Role.Should().Be(MedicalWorkerRole.Other);
    }

    [Fact]
    public async Task CreateWorker_LegacyRequest_ShouldDefaultToOtherWithoutGuessingPosition()
    {
        using var created = await _client.PostAsJsonAsync("/api/medical-workers",
            new SaveMedicalWorkerRequest("А", "Б", "Врач"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<MedicalWorkerDto>())!.Role.Should().Be(MedicalWorkerRole.Other);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Doctor")]
    [InlineData("1")]
    public async Task SaveWorker_UnknownMedicalRole_ShouldReturnBadRequestWithoutWrite(string role)
    {
        var before = await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers?includeDeleted=true");
        using var response = await _client.PostAsJsonAsync("/api/medical-workers", new
        {
            FirstName = "А", LastName = "Б", Position = "Врач", Role = role
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers?includeDeleted=true"))!
            .Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task CreateUpdateDeleteWorker_ValidRequests_ShouldPreserveDeletedRowAndFilterList()
    {
        var create = await _client.PostAsJsonAsync("/api/medical-workers",
            new SaveMedicalWorkerRequest(" Алексей ", "Орлов", "Врач"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var worker = (await create.Content.ReadFromJsonAsync<MedicalWorkerDto>())!;
        create.Headers.Location!.ToString().Should().EndWith($"/api/medical-workers/{worker.Id}");
        worker.FirstName.Should().Be("Алексей");

        var update = await _client.PutAsJsonAsync($"/api/medical-workers/{worker.Id}",
            new SaveMedicalWorkerRequest("Алексей", "Орлов", "Врач-хирург"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<MedicalWorkerDto>())!.Position.Should().Be("Врач-хирург");

        (await _client.DeleteAsync($"/api/medical-workers/{worker.Id}")).StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/medical-workers/{worker.Id}")).StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        var active = await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers");
        active.Should().NotContain(x => x.Id == worker.Id);
        var all = await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers?includeDeleted=true");
        all.Should().Contain(x => x.Id == worker.Id && x.IsDeleted);
        (await _client.GetFromJsonAsync<MedicalWorkerDto>($"/api/medical-workers/{worker.Id}"))!.IsDeleted.Should()
            .BeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var saved = await db.MedicalWorkers.AsNoTracking().SingleAsync(x => x.Id == worker.Id);
        saved.IsDeleted.Should().BeTrue();
        saved.Position.Should().Be("Врач-хирург");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData("1234567890123456789012345")]
    public async Task SaveWorker_InvalidInput_ShouldReturnValidationProblemWithoutInsert(string? name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var count = await db.MedicalWorkers.CountAsync();
        var response = await _client.PostAsJsonAsync("/api/medical-workers",
            new SaveMedicalWorkerRequest(name!, "Орлов", "Врач"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().ContainKey("FirstName");
        (await db.MedicalWorkers.CountAsync()).Should().Be(count);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Send_UnknownWorker_ShouldReturnNotFoundProblem(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/medical-workers/{Guid.NewGuid()}");
        if (method == "PUT")
            request.Content = JsonContent.Create(new SaveMedicalWorkerRequest("Алексей", "Орлов", "Врач"));
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(404);
    }

    [Fact]
    public async Task SaveWorker_DirectDatabaseInsert_ShouldEnforceMinimumLength()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Func<Task> act = async () => await db.Database.ExecuteSqlInterpolatedAsync($"""
             INSERT INTO medical_workers (id, first_name, last_name, position, is_deleted, created_at_utc, updated_at_utc)
             VALUES ({id}, '', 'Орлов', 'Врач', false, {now}, {now})
             """);
        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task InitializeDatabase_RepeatedSeed_ShouldApplyMigrationsAndRemainIdempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        var count = await db.MedicalWorkers.CountAsync();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(seed: true, CancellationToken.None);
        await initializer.InitializeAsync(seed: true, CancellationToken.None);
        (await db.MedicalWorkers.CountAsync()).Should().Be(count);
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
