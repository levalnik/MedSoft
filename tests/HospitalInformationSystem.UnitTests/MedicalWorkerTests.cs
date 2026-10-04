using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers.Services;
using System;
using FluentAssertions;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class MedicalWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_fields_and_stores_utc_timestamps()
    {
        var worker = MedicalWorkerFactory.Create(" Алексей ", " Иванов ", " Врач-терапевт ", Now.ToOffset(TimeSpan.FromHours(3)));
        worker.Id.Should().NotBeEmpty();
        worker.FirstName.Should().Be("Алексей");
        worker.LastName.Should().Be("Иванов");
        worker.Position.Should().Be("Врач-терапевт");
        worker.IsDeleted.Should().BeFalse();
        worker.CreatedAtUtc.Should().Be(Now);
        worker.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        worker.UpdatedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Аб")]
    [InlineData("  Аб  ")]
    [InlineData("1234567890123456789012345")]
    public void Invalid_worker_fields_are_rejected(string? value)
    {
        Action firstName = () => MedicalWorkerFactory.Create(value!, "Иванов", "Врач", Now);
        Action lastName = () => MedicalWorkerFactory.Create("Алексей", value!, "Врач", Now);
        Action position = () => MedicalWorkerFactory.Create("Алексей", "Иванов", value!, Now);
        firstName.Should().Throw<DomainValidationException>();
        lastName.Should().Throw<DomainValidationException>();
        position.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(24)]
    public void Boundary_lengths_are_accepted(int length)
    {
        var text = new string('а', length);
        var worker = MedicalWorkerFactory.Create(text, text, text, Now);
        worker.FirstName.Length.Should().Be(length);
    }

    [Fact]
    public void Invalid_update_does_not_partially_change_entity()
    {
        var worker = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", Now);
        Action update = () => MedicalWorkerService.Update(worker, "Дмитрий", "Петров", "x", Now.AddDays(1));
        update.Should().Throw<DomainValidationException>();
        worker.FirstName.Should().Be("Алексей");
        worker.LastName.Should().Be("Иванов");
        worker.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Deletion_is_idempotent_and_update_does_not_restore_worker()
    {
        var worker = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", Now);
        var originalId = worker.Id;
        MedicalWorkerService.MarkAsDeleted(worker, Now.AddHours(1));
        MedicalWorkerService.MarkAsDeleted(worker, Now.AddHours(2));
        worker.UpdatedAtUtc.Should().Be(Now.AddHours(1));
        MedicalWorkerService.Update(worker, "Дмитрий", "Иванов", "Врач-хирург", Now.AddHours(3));
        worker.Id.Should().Be(originalId);
        worker.IsDeleted.Should().BeTrue();
        worker.CreatedAtUtc.Should().Be(Now);
    }
}
