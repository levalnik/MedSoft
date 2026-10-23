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
    public void Create_ValidFields_ShouldTrimTextAndStoreUtcTimestamps()
    {
        var worker = MedicalWorkerFactory.Create(" Алексей ", " Иванов ", " Врач-терапевт ",
            Now.ToOffset(TimeSpan.FromHours(3)));
        worker.Id.Should().NotBeEmpty();
        worker.FirstName.Should().Be("Алексей");
        worker.LastName.Should().Be("Иванов");
        worker.Position.Should().Be("Врач-терапевт");
        worker.IsDeleted.Should().BeFalse();
        worker.Role.Should().Be(MedicalWorkerRole.Other);
        worker.CreatedAtUtc.Should().Be(Now);
        worker.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        worker.UpdatedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345")]
    public void Create_InvalidFields_ShouldRejectInput(string? value)
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
    public void Create_BoundaryLengths_ShouldAcceptInput(int length)
    {
        var text = new string('а', length);
        var worker = MedicalWorkerFactory.Create(text, text, text, Now);
        worker.FirstName.Length.Should().Be(length);
    }

    [Theory]
    [InlineData("А", "Б")]
    [InlineData(" Аб ", " Вг ")]
    public void Create_ShortNamesAndPosition_ShouldAcceptNamesAndRejectPosition(string firstName, string lastName)
    {
        var worker = MedicalWorkerFactory.Create(firstName, lastName, "Врач", Now);
        worker.FirstName.Should().Be(firstName.Trim());
        worker.LastName.Should().Be(lastName.Trim());
        var create = () => MedicalWorkerFactory.Create(firstName, lastName, "Аб", Now);
        create.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(MedicalWorkerRole.Doctor)]
    [InlineData(MedicalWorkerRole.Nurse)]
    [InlineData(MedicalWorkerRole.LaboratoryTechnician)]
    [InlineData(MedicalWorkerRole.Other)]
    public void CreateAndUpdate_ExplicitRole_ShouldPreserveRole(MedicalWorkerRole role)
    {
        var worker = MedicalWorkerFactory.Create("А", "Б", "Врач", Now, role);
        worker.Role.Should().Be(role);
        MedicalWorkerService.Update(worker, "В", "Г", "Хирург", Now.AddHours(1));
        worker.Role.Should().Be(role);
        MedicalWorkerService.Update(worker, "В", "Г", "Хирург", Now.AddHours(2), MedicalWorkerRole.Other);
        worker.Role.Should().Be(MedicalWorkerRole.Other);
    }

    [Fact]
    public void Update_InvalidRole_ShouldPreserveEntity()
    {
        var worker = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", Now, MedicalWorkerRole.Doctor);
        var update = () => MedicalWorkerService.Update(worker, "Мария", "Петрова", "Сестра", Now.AddDays(1),
            (MedicalWorkerRole)99);
        update.Should().Throw<DomainValidationException>();
        worker.FirstName.Should().Be("Алексей");
        worker.LastName.Should().Be("Иванов");
        worker.Position.Should().Be("Врач");
        worker.Role.Should().Be(MedicalWorkerRole.Doctor);
        worker.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Update_InvalidFields_ShouldPreserveEntity()
    {
        var worker = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", Now);
        Action update = () => MedicalWorkerService.Update(worker, "Дмитрий", "Петров", "x", Now.AddDays(1));
        update.Should().Throw<DomainValidationException>();
        worker.FirstName.Should().Be("Алексей");
        worker.LastName.Should().Be("Иванов");
        worker.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Delete_RepeatedDeletionAndUpdate_ShouldRemainDeleted()
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
