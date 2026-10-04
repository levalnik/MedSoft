using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;
using HospitalInformationSystem.Application.Patients.Commands.DeletePatient;
using HospitalInformationSystem.Application.Patients.Commands.UpdatePatient;
using HospitalInformationSystem.Application.Patients.Queries.GetPatients;
using HospitalInformationSystem.Domain.Diagnoses;
using HospitalInformationSystem.Domain.Patients;
using Moq;
using Xunit;
using ContractSex = HospitalInformationSystem.Contracts.Patients.AdministrativeSex;

namespace HospitalInformationSystem.UnitTests;

public sealed class PatientAndDiagnosisUseCaseTests
{
    private static Patient NewPatient() =>
        Patient.Create("Елена", "Соколова", null, new DateOnly(1985, 3, 12),
            AdministrativeSex.Female, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Patient_with_record_cannot_be_deleted()
    {
        var patient = NewPatient();
        var repository = new Mock<IPatientRepository>();
        repository.Setup(x => x.FindAsync(patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var presence = new Mock<IMedicalRecordPresence>();
        presence.Setup(x => x.ExistsForPatientAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeletePatientCommandHandler(repository.Object, presence.Object, unitOfWork.Object);

        var act = () => handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessConflictException>();
        repository.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Patient_without_record_is_physically_deleted_and_saved()
    {
        var patient = NewPatient();
        var repository = new Mock<IPatientRepository>();
        repository.Setup(x => x.FindAsync(patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var presence = new Mock<IMedicalRecordPresence>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeletePatientCommandHandler(repository.Object, presence.Object, unitOfWork.Object);

        await handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        repository.Verify(x => x.DeleteAsync(patient.Id, CancellationToken.None), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task List_enriches_patient_status_in_one_presence_query()
    {
        var first = NewPatient();
        var second = NewPatient();
        var repository = new Mock<IPatientRepository>();
        repository.Setup(x => x.ListAsync(CancellationToken.None)).ReturnsAsync(new[] { first, second });
        var presence = new Mock<IMedicalRecordPresence>();
        presence.Setup(x => x.GetPatientIdsWithRecordsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(first.Id) && ids.Contains(second.Id)),
            CancellationToken.None)).ReturnsAsync(new HashSet<Guid> { first.Id });
        var handler = new GetPatientsQueryHandler(repository.Object, presence.Object);

        var result = await handler.Handle(new GetPatientsQuery(), CancellationToken.None);

        result.Single(x => x.Id == first.Id).HasMedicalRecord.Should().BeTrue();
        result.Single(x => x.Id == second.Id).HasMedicalRecord.Should().BeFalse();
        result.First().Sex.Should().Be(ContractSex.Female);
        presence.Verify(x => x.ExistsForPatientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_explicitly_copies_domain_changes_to_repository_before_saving()
    {
        var patient = NewPatient();
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(now);
        var repository = new Mock<IPatientRepository>(MockBehavior.Strict);
        repository.Setup(x => x.FindAsync(patient.Id, true, CancellationToken.None)).ReturnsAsync(patient);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var presence = new Mock<IMedicalRecordPresence>();
        presence.Setup(x => x.ExistsForPatientAsync(patient.Id, CancellationToken.None)).ReturnsAsync(true);
        var sequence = new MockSequence();
        repository.InSequence(sequence).Setup(x => x.UpdateAsync(patient, CancellationToken.None)).Returns(Task.CompletedTask);
        unitOfWork.InSequence(sequence).Setup(x => x.SaveChangesAsync(CancellationToken.None)).ReturnsAsync(1);
        var handler = new UpdatePatientCommandHandler(repository.Object, unitOfWork.Object, clock.Object, presence.Object);

        var result = await handler.Handle(new UpdatePatientCommand(patient.Id, "Мария", "Петрова", null,
            patient.BirthDate, ContractSex.Unknown, "Комментарий"), CancellationToken.None);

        result.FirstName.Should().Be("Мария");
        result.Sex.Should().Be(ContractSex.Unknown);
        result.HasMedicalRecord.Should().BeTrue();
        patient.UpdatedAtUtc.Should().Be(now);
        repository.Verify(x => x.UpdateAsync(patient, CancellationToken.None), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Duplicate_diagnosis_is_rejected_without_save()
    {
        var repository = new Mock<IDiagnosisRepository>();
        repository.Setup(x => x.ExistsConflictAsync("A00.00", "Холера", null, CancellationToken.None)).ReturnsAsync(true);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateDiagnosisCommandHandler(repository.Object, unitOfWork.Object, TimeProvider.System);

        var act = () => handler.Handle(new CreateDiagnosisCommand(" A00.00 ", " Холера "), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessConflictException>();
        repository.Verify(x => x.Add(It.IsAny<DiagnosisDefinition>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
