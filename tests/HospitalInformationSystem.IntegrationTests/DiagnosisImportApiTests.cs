using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class DiagnosisImportApiTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Import_creates_updates_and_repeats_without_changing_ids_or_timestamps()
    {
        var code = Guid.NewGuid().ToString("N")[..6];
        var secondCode = Guid.NewGuid().ToString("N")[..6];
        var name = "Диагноз " + code;
        var initial = await ImportAsync($"\uFEFFCode,Name\r\n{code},{name}\r\n");
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        (await initial.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should().Be(new DiagnosisImportResultDto(1, 0, 0));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var before = await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code);

        var csv = $"Code,Name\n{code},Новое {code}\n{secondCode},\"Боль, \"\"{secondCode}\"\"\"";
        var update = await ImportAsync(csv);
        (await update.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should().Be(new DiagnosisImportResultDto(1, 1, 0));
        var updated = await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code);
        updated.Id.Should().Be(before.Id);
        updated.CreatedAtUtc.Should().Be(before.CreatedAtUtc);
        updated.Name.Should().Be("Новое " + code);
        var repeated = await ImportAsync(csv);
        (await repeated.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should().Be(new DiagnosisImportResultDto(0, 0, 2));
        (await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code)).UpdatedAtUtc.Should().Be(updated.UpdatedAtUtc);
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses"))!
            .Should().Contain(x => x.Code == secondCode && x.Name == $"Боль, \"{secondCode}\"");
    }

    [Theory]
    [InlineData("Code,Name\nT10001,Первый\nBAD,Ошибка")]
    [InlineData("Code,Name\nT10002,Первый\n T10002 ,Второй")]
    [InlineData("Code,Name\nT10003,Первый\nT10004, Первый ")]
    [InlineData("Code,Name\nT10005,Первый\nT10006,\"Незакрыто")]
    public async Task Invalid_file_returns_400_and_leaves_database_unchanged(string csv)
    {
        var before = await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses");
        var response = await ImportAsync(csv);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses")).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Database_name_conflict_does_not_save_earlier_update_or_insert()
    {
        var token = Guid.NewGuid().ToString("N");
        var code = token[..6];
        var other = token[6..12];
        var added = token[12..18];
        (await ImportAsync($"Code,Name\n{code},Старое {code}\n{other},Занято {code}")).EnsureSuccessStatusCode();
        var before = await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses");
        var response = await ImportAsync($"Code,Name\n{code},Новое {code}\n{added},Занято {code}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("Строка 3");
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses")).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Database_failure_rolls_back_updates_and_inserts_across_batches()
    {
        // A real PostgreSQL constraint failure during SaveChanges, after validation.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await ImportAsync("Code,Name\nR00000,До импорта")).EnsureSuccessStatusCode();
        var before = await db.DiagnosisDefinitions.AsNoTracking().ToListAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_csv_row() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.code = 'R00060' THEN
                    RAISE EXCEPTION 'CSV test conflict' USING ERRCODE = '23505';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_csv_row BEFORE INSERT ON diagnosis_definitions
                FOR EACH ROW EXECUTE FUNCTION reject_csv_row();
            """);
        try
        {
            var csv = "Code,Name\nR00000,После импорта\n" +
                string.Join("\n", Enumerable.Range(1, 60).Select(i => $"R{i:D5},Откат {i:D5}"));
            var response = await ImportAsync(csv);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await db.DiagnosisDefinitions.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(before);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_csv_row ON diagnosis_definitions; DROP FUNCTION reject_csv_row();");
        }
    }

    [Fact]
    public async Task Missing_empty_non_utf8_and_oversized_files_are_rejected()
    {
        using var missing = new MultipartFormDataContent();
        (await _client.PostAsync("/api/diagnoses/import", missing)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportAsync("")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportBytesAsync([0xff, 0xfe, 0x41, 0x00])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportBytesAsync(new byte[DiagnosisImportLimits.MaxFileBytes + 1])).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private Task<HttpResponseMessage> ImportAsync(string csv) => ImportBytesAsync(Encoding.UTF8.GetBytes(csv));

    private async Task<HttpResponseMessage> ImportBytesAsync(byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", "diagnoses.csv");
        return await _client.PostAsync("/api/diagnoses/import", content);
    }
}
