using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Persistence;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalWorkerPersistenceTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    [Fact]
    public async Task Domain_changes_survive_dbo_round_trips_without_resetting_identity_or_timestamps()
    {
        var createdAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var deletedAt = createdAt.AddHours(2);
        var worker = MedicalWorker.Create("Алексей", "Орлов", "Врач", createdAt);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            db.Model.FindEntityType(typeof(MedicalWorker)).Should().BeNull();
            db.Model.FindEntityType(typeof(MedicalWorkerDbo)).Should().NotBeNull();
            var repository = scope.ServiceProvider.GetRequiredService<IMedicalWorkerRepository>();
            repository.Add(worker);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMedicalWorkerRepository>();
            var restored = (await repository.FindAsync(worker.Id, tracking: true, CancellationToken.None))!;
            restored.Should().BeEquivalentTo(worker);
            restored.Update("Дмитрий", "Петров", "Врач-хирург", updatedAt);
            await repository.UpdateAsync(restored, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMedicalWorkerRepository>();
            var restored = (await repository.FindAsync(worker.Id, tracking: true, CancellationToken.None))!;
            restored.FirstName.Should().Be("Дмитрий");
            restored.LastName.Should().Be("Петров");
            restored.Position.Should().Be("Врач-хирург");
            restored.CreatedAtUtc.Should().Be(createdAt);
            restored.UpdatedAtUtc.Should().Be(updatedAt);
            restored.MarkAsDeleted(deletedAt);
            await repository.UpdateAsync(restored, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<IMedicalWorkerRepository>();
            var restored = (await repository.FindAsync(worker.Id, tracking: false, CancellationToken.None))!;
            db.ChangeTracker.Entries().Should().BeEmpty();
            restored.IsDeleted.Should().BeTrue();
            restored.CreatedAtUtc.Should().Be(createdAt);
            restored.UpdatedAtUtc.Should().Be(deletedAt);
            restored.MarkAsDeleted(deletedAt.AddHours(1));
            restored.UpdatedAtUtc.Should().Be(deletedAt);
            // UpdateAsync также умеет загрузить DBO, если чтение было без tracking.
            await repository.UpdateAsync(restored, CancellationToken.None);
            (await db.SaveChangesAsync()).Should().Be(0);
        }
    }
}
