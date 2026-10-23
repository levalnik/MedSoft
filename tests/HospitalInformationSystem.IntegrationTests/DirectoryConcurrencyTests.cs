using System;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class DirectoryConcurrencyTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    [Theory]
    [InlineData("worker", false, false)]
    [InlineData("patient", false, false)]
    [InlineData("diagnosis", false, false)]
    [InlineData("worker", true, false)]
    [InlineData("patient", true, false)]
    [InlineData("diagnosis", true, false)]
    [InlineData("worker", false, true)]
    [InlineData("patient", false, true)]
    [InlineData("diagnosis", false, true)]
    [InlineData("worker", true, true)]
    [InlineData("patient", true, true)]
    [InlineData("diagnosis", true, true)]
    public async Task UpdateOrDelete_StaleVersion_ShouldConflictAndPreserveWinningChange(
        string kind, bool delete, bool saveSynchronously)
    {
        var id = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var (row, property) = CreateRow(kind, id, now);
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var setup = setupScope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            setup.Add(row);
            await setup.SaveChangesAsync();
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var winner = (await first.FindAsync(row.GetType(), id))!;
        var loser = (await second.FindAsync(row.GetType(), id))!;
        var winnerValue = "Победа " + id.ToString("N")[..12];
        first.Entry(winner).Property(property).CurrentValue = winnerValue;
        if (delete)
            second.Remove(loser);
        else
            second.Entry(loser).Property(property).CurrentValue = "Устаревшее изменение";

        await first.SaveChangesAsync();
        if (saveSynchronously)
        {
            Action saveStale = () => second.SaveChanges();
            saveStale.Should().Throw<BusinessConflictException>();
        }
        else
        {
            var saveStale = () => second.SaveChangesAsync();
            await saveStale.Should().ThrowAsync<BusinessConflictException>();
        }
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var saved = (await verification.FindAsync(row.GetType(), id))!;
        verification.Entry(saved).Property(property).CurrentValue.Should().Be(winnerValue);
        verification.Entry(saved).Property("UpdatedAtUtc").CurrentValue.Should().Be(now);
    }

    [Theory]
    [InlineData("unique", false)]
    [InlineData("check", false)]
    [InlineData("foreign-key", false)]
    [InlineData("unique", true)]
    [InlineData("check", true)]
    [InlineData("foreign-key", true)]
    public async Task Save_DatabaseConstraintViolation_ShouldReturnBusinessConflict(
        string constraint, bool saveSynchronously)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var (row, _) = CreateRow("diagnosis", id, now);
        var diagnosis = (DiagnosisDefinitionDbo)row;
        switch (constraint)
        {
            case "unique":
                var (duplicate, _) = CreateRow("diagnosis", Guid.NewGuid(), now);
                ((DiagnosisDefinitionDbo)duplicate).Code = diagnosis.Code;
                db.AddRange(diagnosis, duplicate);
                break;
            case "check":
                diagnosis.Name = "ab";
                db.Add(diagnosis);
                break;
            case "foreign-key":
                db.Add(new MedicalRecordDbo
                {
                    Id = id, PatientId = Guid.NewGuid(), CreatedAtUtc = now, Version = 1
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(constraint));
        }

        if (saveSynchronously)
        {
            Action save = () => db.SaveChanges();
            save.Should().Throw<BusinessConflictException>();
        }
        else
        {
            var save = () => db.SaveChangesAsync();
            await save.Should().ThrowAsync<BusinessConflictException>();
        }
    }

    private static (object Row, string Property) CreateRow(string kind, Guid id, DateTimeOffset now) => kind switch
    {
        "worker" => (new MedicalWorkerDbo
        {
            Id = id, FirstName = "Алексей", LastName = "Иванов", Position = "Врач",
            CreatedAtUtc = now, UpdatedAtUtc = now
        }, nameof(MedicalWorkerDbo.FirstName)),
        "patient" => (new PatientDbo
        {
            Id = id, FirstName = "Алексей", LastName = "Иванов", BirthDate = new DateOnly(1990, 1, 1),
            CreatedAtUtc = now, UpdatedAtUtc = now
        }, nameof(PatientDbo.FirstName)),
        "diagnosis" => (new DiagnosisDefinitionDbo
        {
            Id = id, Code = id.ToString("N")[..6], Name = "Диагноз " + id.ToString("N")[..12],
            CreatedAtUtc = now, UpdatedAtUtc = now
        }, nameof(DiagnosisDefinitionDbo.Name)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
