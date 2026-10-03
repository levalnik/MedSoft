using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.DeleteMedicalWorker;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Moq;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class MedicalWorkerUseCaseTests
{
    [Fact]
    public async Task Creation_uses_injected_clock_and_saves_the_new_worker()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(now);
        var repository = new Mock<IMedicalWorkerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        MedicalWorker? saved = null;
        repository.Setup(x => x.Add(It.IsAny<MedicalWorker>())).Callback<MedicalWorker>(worker => saved = worker);
        var handler = new CreateMedicalWorkerCommandHandler(repository.Object, unitOfWork.Object, clock.Object);

        var result = await handler.Handle(new CreateMedicalWorkerCommand("Алексей", "Иванов", "Врач"), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.CreatedAtUtc.Should().Be(now);
        result.Id.Should().Be(saved.Id);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Deleting_unknown_worker_does_not_save_any_changes()
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
    [InlineData(" А ")]
    [InlineData("1234567890123456789012345")]
    public async Task Command_validation_catches_invalid_input_before_handler(string? name)
    {
        var validator = new CreateMedicalWorkerCommandValidator();
        var result = await validator.ValidateAsync(new CreateMedicalWorkerCommand(name!, "Иванов", "Врач"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "FirstName");
    }
}
