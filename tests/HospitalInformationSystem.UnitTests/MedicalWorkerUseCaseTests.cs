using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.DeleteMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using ContractRole = HospitalInformationSystem.Contracts.Security.MedicalWorkerRole;
using Moq;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class MedicalWorkerUseCaseTests
{
    [Fact]
    public async Task Handle_ValidCommand_ShouldUseInjectedClockAndSaveWorker()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(now);
        var repository = new Mock<IMedicalWorkerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        MedicalWorker? saved = null;
        repository.Setup(x => x.Add(It.IsAny<MedicalWorker>())).Callback<MedicalWorker>(worker => saved = worker);
        var handler = new CreateMedicalWorkerCommandHandler(repository.Object, unitOfWork.Object, clock.Object);

        var result = await handler.Handle(new CreateMedicalWorkerCommand("Алексей", "Иванов", "Врач"),
            CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.CreatedAtUtc.Should().Be(now);
        result.Id.Should().Be(saved.Id);
        result.Role.Should().Be(ContractRole.Other);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownWorker_ShouldNotSaveChanges()
    {
        var repository = new Mock<IMedicalWorkerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteMedicalWorkerCommandHandler(repository.Object, unitOfWork.Object, TimeProvider.System);
        var act = () => handler.Handle(new DeleteMedicalWorkerCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<EntityNotFoundException>();
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("1234567890123456789012345")]
    public async Task ValidateAsync_InvalidInput_ShouldRejectBeforeHandler(string? name)
    {
        var validator = new CreateMedicalWorkerCommandValidator();
        var result = await validator.ValidateAsync(new CreateMedicalWorkerCommand(name!, "Иванов", "Врач"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData(ContractRole.Doctor, MedicalWorkerRole.Doctor)]
    [InlineData(ContractRole.LaboratoryTechnician, MedicalWorkerRole.LaboratoryTechnician)]
    [InlineData(ContractRole.Nurse, MedicalWorkerRole.Nurse)]
    [InlineData(ContractRole.Other, MedicalWorkerRole.Other)]
    public async Task Handle_ExplicitRole_ShouldMapRoleInBothDirections(ContractRole role,
        MedicalWorkerRole domainRole)
    {
        var repository = new Mock<IMedicalWorkerRepository>();
        MedicalWorker? saved = null;
        repository.Setup(x => x.Add(It.IsAny<MedicalWorker>())).Callback<MedicalWorker>(worker => saved = worker);
        var handler = new CreateMedicalWorkerCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(),
            TimeProvider.System);
        var result = await handler.Handle(new CreateMedicalWorkerCommand("А", "Б", "Врач", role),
            CancellationToken.None);
        saved!.Role.Should().Be(domainRole);
        result.Role.Should().Be(role);
    }

    [Theory]
    [InlineData(null, MedicalWorkerRole.Doctor, ContractRole.Doctor)]
    [InlineData(ContractRole.Nurse, MedicalWorkerRole.Nurse, ContractRole.Nurse)]
    [InlineData(ContractRole.Other, MedicalWorkerRole.Other, ContractRole.Other)]
    public async Task Handle_OptionalRole_ShouldChangeRoleOnlyWhenProvided(ContractRole? requestedRole,
        MedicalWorkerRole expectedDomainRole, ContractRole expectedDtoRole)
    {
        var worker = MedicalWorkerFactory.Create("А", "Б", "Врач", DateTimeOffset.UtcNow, MedicalWorkerRole.Doctor);
        var repository = new Mock<IMedicalWorkerRepository>();
        repository.Setup(x => x.FindAsync(worker.Id, true, CancellationToken.None)).ReturnsAsync(worker);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new UpdateMedicalWorkerCommandHandler(repository.Object, unitOfWork.Object, TimeProvider.System);
        var result = await handler.Handle(new UpdateMedicalWorkerCommand(worker.Id, "В", "Г", "Хирург", requestedRole),
            CancellationToken.None);
        worker.Role.Should().Be(expectedDomainRole);
        result.Role.Should().Be(expectedDtoRole);
        repository.Verify(x => x.UpdateAsync(worker, CancellationToken.None), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ContractRole.Doctor)]
    public async Task ValidateAsync_SingleLetterNamesAndOptionalRole_ShouldAcceptInput(ContractRole? role)
    {
        var create = await new CreateMedicalWorkerCommandValidator()
            .ValidateAsync(new CreateMedicalWorkerCommand(" А ", "Б", "Врач", role));
        var update = await new UpdateMedicalWorkerCommandValidator()
            .ValidateAsync(new UpdateMedicalWorkerCommand(Guid.NewGuid(), "А", " Б ", "Врач", role));
        create.IsValid.Should().BeTrue();
        update.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_UnknownRoleAndShortPosition_ShouldRejectInput()
    {
        var create = await new CreateMedicalWorkerCommandValidator()
            .ValidateAsync(new CreateMedicalWorkerCommand("А", "Б", "Аб", (ContractRole)99));
        var update = await new UpdateMedicalWorkerCommandValidator()
            .ValidateAsync(new UpdateMedicalWorkerCommand(Guid.NewGuid(), "А", "Б", "Аб", (ContractRole)99));
        create.Errors.Should().Contain(x => x.PropertyName == "Role").And.Contain(x => x.PropertyName == "Position");
        update.Errors.Should().Contain(x => x.PropertyName == "Role").And.Contain(x => x.PropertyName == "Position");
    }
}
