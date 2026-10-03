using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Infrastructure.Persistence;
using HospitalInformationSystem.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class PersistenceMigrationTests
{
    [Fact]
    public async Task Moving_to_dbo_preserves_data_written_before_the_migration()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<HospitalDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new HospitalDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261002171752_InitialCreate");

        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var deletedAt = createdAt.AddHours(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO medical_workers (id, first_name, last_name, position, is_deleted, created_at_utc, updated_at_utc)
            VALUES ({id}, 'Алексей', 'Орлов', 'Врач', true, {createdAt}, {deletedAt})
            """);

        await migrator.MigrateAsync();

        var restored = (await new MedicalWorkerRepository(db).FindAsync(id, tracking: false, CancellationToken.None))!;
        restored.Id.Should().Be(id);
        restored.FirstName.Should().Be("Алексей");
        restored.LastName.Should().Be("Орлов");
        restored.Position.Should().Be("Врач");
        restored.IsDeleted.Should().BeTrue();
        restored.CreatedAtUtc.Should().Be(createdAt);
        restored.UpdatedAtUtc.Should().Be(deletedAt);
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
