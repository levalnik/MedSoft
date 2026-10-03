using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalWorkersApiTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_update_delete_preserves_row_and_filters_default_list()
    {
        var create = await _client.PostAsJsonAsync("/api/medical-workers", new SaveMedicalWorkerRequest(" Алексей ", "Орлов", "Врач"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var worker = (await create.Content.ReadFromJsonAsync<MedicalWorkerDto>())!;
        create.Headers.Location!.ToString().Should().EndWith($"/api/medical-workers/{worker.Id}");
        worker.FirstName.Should().Be("Алексей");

        var update = await _client.PutAsJsonAsync($"/api/medical-workers/{worker.Id}", new SaveMedicalWorkerRequest("Алексей", "Орлов", "Врач-хирург"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<MedicalWorkerDto>())!.Position.Should().Be("Врач-хирург");

        (await _client.DeleteAsync($"/api/medical-workers/{worker.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/medical-workers/{worker.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var active = await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers");
        active.Should().NotContain(x => x.Id == worker.Id);
        var all = await _client.GetFromJsonAsync<MedicalWorkerDto[]>("/api/medical-workers?includeDeleted=true");
        all.Should().Contain(x => x.Id == worker.Id && x.IsDeleted);
        (await _client.GetFromJsonAsync<MedicalWorkerDto>($"/api/medical-workers/{worker.Id}"))!.IsDeleted.Should().BeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await db.MedicalWorkers.AsNoTracking().SingleAsync(x => x.Id == worker.Id)).IsDeleted.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("Аб")]
    [InlineData("1234567890123456789012345")]
    public async Task Invalid_input_returns_validation_problem_and_does_not_insert(string? name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var count = await db.MedicalWorkers.CountAsync();
        var response = await _client.PostAsJsonAsync("/api/medical-workers", new SaveMedicalWorkerRequest(name!, "Орлов", "Врач"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().ContainKey("FirstName");
        (await db.MedicalWorkers.CountAsync()).Should().Be(count);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Missing_worker_returns_404_problem_details(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/medical-workers/{Guid.NewGuid()}");
        if (method == "PUT") request.Content = JsonContent.Create(new SaveMedicalWorkerRequest("Алексей", "Орлов", "Врач"));
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(404);
    }

    [Fact]
    public async Task PostgreSql_enforces_minimum_length_even_without_application_validation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Func<Task> act = async () => await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO medical_workers (id, first_name, last_name, position, is_deleted, created_at_utc, updated_at_utc)
            VALUES ({id}, 'А', 'Орлов', 'Врач', false, {now}, {now})
            """);
        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Migrations_are_applied_and_seed_is_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        var count = await db.MedicalWorkers.CountAsync();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(seed: true);
        await initializer.InitializeAsync(seed: true);
        (await db.MedicalWorkers.CountAsync()).Should().Be(count);
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
