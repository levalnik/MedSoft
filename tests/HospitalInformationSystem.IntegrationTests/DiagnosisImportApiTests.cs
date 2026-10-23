using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class DiagnosisImportApiTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateBusinessClient();

    [Fact]
    public async Task Import_ValidRepeatedFile_ShouldCreateUpdateAndPreserveIdentityAndTimestamps()
    {
        var code = Guid.NewGuid().ToString("N")[..6];
        var secondCode = Guid.NewGuid().ToString("N")[..6];
        var name = "Диагноз " + code;
        var initial = await ImportAsync($"\uFEFFCode,Name\r\n{code},{name}\r\n");
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        (await initial.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should()
            .Be(new DiagnosisImportResultDto(1, 0, 0));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var before = await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code);

        var csv = $"Code,Name\n{code},Новое {code}\n{secondCode},\"Боль, \"\"{secondCode}\"\"\"";
        var update = await ImportAsync(csv);
        (await update.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should()
            .Be(new DiagnosisImportResultDto(1, 1, 0));
        var updated = await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code);
        updated.Id.Should().Be(before.Id);
        updated.CreatedAtUtc.Should().Be(before.CreatedAtUtc);
        updated.Name.Should().Be("Новое " + code);
        var repeated = await ImportAsync(csv);
        (await repeated.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should()
            .Be(new DiagnosisImportResultDto(0, 0, 2));
        (await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Code == code)).UpdatedAtUtc.Should()
            .Be(updated.UpdatedAtUtc);
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses"))!
            .Should().Contain(x => x.Code == secondCode && x.Name == $"Боль, \"{secondCode}\"");
    }

    [Theory]
    [InlineData("Code,Name\nT10001,Первый\nBAD,Ошибка")]
    [InlineData("Code,Name\nT10002,Первый\n T10002 ,Второй")]
    [InlineData("Code,Name\nT10003,Первый\nT10004, Первый ")]
    [InlineData("Code,Name\nT10005,Первый\nT10006,\"Незакрыто")]
    public async Task Import_InvalidFile_ShouldReturnBadRequestWithoutDatabaseChanges(string csv)
    {
        var before = await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses");
        var response = await ImportAsync(csv);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses")).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Import_DatabaseNameConflict_ShouldRollBackEarlierUpdatesAndInserts()
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
    public async Task Import_DatabaseFailure_ShouldRollBackChangesAcrossBatches()
    {
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
            await db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER reject_csv_row ON diagnosis_definitions; DROP FUNCTION reject_csv_row();");
        }
    }

    [Fact]
    public async Task Import_MissingEmptyInvalidEncodingOrOversizedFile_ShouldRejectFile()
    {
        using var missing = new MultipartFormDataContent();
        (await _client.PostAsync("/api/diagnoses/import", missing)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportAsync("")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportBytesAsync([0xff, 0xfe, 0x41, 0x00])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ImportBytesAsync(new byte[DiagnosisImportLimits.MaxFileBytes + 1])).StatusCode.Should()
            .Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Import_ManyUpdatedRows_ShouldReadCatalogOnce()
    {
        var codes = Enumerable.Range(0, 25).Select(i => $"Q{i:D5}").ToArray();
        (await ImportAsync("Code,Name\n" + string.Join("\n", codes.Select(code => $"{code},До {code}"))))
            .EnsureSuccessStatusCode();
        var reads = new DiagnosisReadInterceptor();
        await using var observedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddDbContext<HospitalDbContext>(options => options.AddInterceptors(reads))));
        using var client = observedFactory.CreateBusinessClient();
        reads.Reset();

        var response =
            await ImportAsync("Code,Name\n" + string.Join("\n", codes.Select(code => $"{code},После {code}")), client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DiagnosisImportResultDto>()).Should()
            .Be(new DiagnosisImportResultDto(0, codes.Length, 0));
        reads.Count.Should().Be(1, "the number of catalog reads must not grow with the imported row count");
    }

    [Fact]
    public async Task Import_ConcurrentFilesWithSameNewCode_ShouldRollBackLosingFile()
    {
        const string sharedCode = "Y00001";
        const string firstCode = "Y00002";
        const string secondCode = "Y00003";
        var reads = new DiagnosisReadInterceptor();
        await using var observedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddDbContext<HospitalDbContext>(options => options.AddInterceptors(reads))));
        using var client = observedFactory.CreateBusinessClient();
        reads.WaitForPair();

        var responses = await Task.WhenAll(
            ImportAsync($"Code,Name\n{sharedCode},Первый {sharedCode}\n{firstCode},Первый {firstCode}", client),
            ImportAsync($"Code,Name\n{sharedCode},Второй {sharedCode}\n{secondCode},Второй {secondCode}", client));

        responses.Select(response => response.StatusCode).Should()
            .BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var winner = responses[0].IsSuccessStatusCode ? firstCode : secondCode;
        var loser = responses[0].IsSuccessStatusCode ? secondCode : firstCode;
        var diagnoses = (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses"))!;
        diagnoses.Should().ContainSingle(x => x.Code == sharedCode);
        diagnoses.Should().ContainSingle(x => x.Code == winner);
        diagnoses.Should().NotContain(x => x.Code == loser);
    }

    private Task<HttpResponseMessage> ImportAsync(string csv, HttpClient? client = null) =>
        ImportBytesAsync(Encoding.UTF8.GetBytes(csv), client);

    private async Task<HttpResponseMessage> ImportBytesAsync(byte[] bytes, HttpClient? client = null)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", "diagnoses.csv");
        return await (client ?? _client).PostAsync("/api/diagnoses/import", content);
    }

    private sealed class DiagnosisReadInterceptor : DbCommandInterceptor
    {
        private int _count;
        private TaskCompletionSource? _pair;

        public int Count => Volatile.Read(ref _count);
        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public void WaitForPair()
        {
            Reset();
            _pair = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("FROM diagnosis_definitions", StringComparison.Ordinal))
            {
                var position = Interlocked.Increment(ref _count);
                if (_pair is not null && position <= 2)
                {
                    if (position == 2) _pair.TrySetResult();
                    await _pair.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }

            return result;
        }
    }
}
