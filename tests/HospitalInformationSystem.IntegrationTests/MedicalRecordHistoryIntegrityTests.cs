using System;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalRecordHistoryIntegrityTests
{
    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task Existing_history_cannot_be_updated_or_deleted_through_async_unit_of_work(EntityState state)
    {
        await using var db = CreateContext();
        db.Entry(ExistingEvent()).State = state;

        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*истории*нельзя*");
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void Existing_history_cannot_be_updated_or_deleted_through_sync_unit_of_work(EntityState state)
    {
        using var db = CreateContext();
        db.Entry(ExistingEvent()).State = state;

        var save = () => db.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*истории*нельзя*");
    }

    private static HospitalDbContext CreateContext() => new(new DbContextOptionsBuilder<HospitalDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1")
        .Options);

    private static MedicalRecordHistoryEventDbo ExistingEvent() => new()
    {
        Id = Guid.NewGuid(), MedicalRecordId = Guid.NewGuid(), Version = 1,
        OccurredAtUtc = DateTimeOffset.UnixEpoch, PerformedByMedicalWorkerId = Guid.NewGuid(),
        PerformedByNameSnapshot = "Иванов Алексей", PerformedByPositionSnapshot = "Врач",
        EventType = "MedicalRecordCreated", EntityId = Guid.NewGuid(), PayloadJson = "{}"
    };
}
