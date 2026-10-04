using HospitalInformationSystem.Domain.MedicalRecords.Factories;
using HospitalInformationSystem.Domain.MedicalRecords.Services;
using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers.Services;
using System;
using System.Linq;
using FluentAssertions;
using HospitalInformationSystem.Application.MedicalRecords;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalRecords;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class MedicalRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static MedicalWorker Author() => MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", Now);

    [Fact]
    public void Creation_records_patient_and_author_snapshot_in_utc()
    {
        var author = Author();
        var patientId = Guid.NewGuid();
        var record = MedicalRecordFactory.Create(patientId, author, Now.ToOffset(TimeSpan.FromHours(3)));

        record.Id.Should().NotBeEmpty();
        record.PatientId.Should().Be(patientId);
        record.CreatedAtUtc.Should().Be(Now);
        record.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        record.Version.Should().Be(1);
        var history = record.HistoryEvents.Should().ContainSingle().Subject;
        history.EventType.Should().Be(MedicalRecordEventType.MedicalRecordCreated);
        history.PerformedByMedicalWorkerId.Should().Be(author.Id);
        history.PerformedByNameSnapshot.Should().Be("Иванов Алексей");
        history.PayloadJson.Should().Contain($"\"patientId\":\"{patientId}\"");

        MedicalWorkerService.Update(author, "Дмитрий", "Петров", "Хирург", Now.AddDays(1));
        MedicalWorkerService.MarkAsDeleted(author, Now.AddDays(2));
        history.PerformedByNameSnapshot.Should().Be("Иванов Алексей");
        history.PerformedByPositionSnapshot.Should().Be("Врач");
    }

    [Fact]
    public void Replacement_preserves_original_text_and_emits_two_ordered_events_at_same_time()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var original = MedicalRecordService.AddPrescription(record, "Исходное назначение", author, Now.AddMinutes(1));
        var replacedAt = Now.AddMinutes(2);
        var replacement = MedicalRecordService.ReplacePrescription(record, original.Id, " Новое назначение ", author, replacedAt);

        original.Text.Should().Be("Исходное назначение");
        original.CancelledAtUtc.Should().Be(replacedAt);
        original.CancelledByMedicalWorkerId.Should().Be(author.Id);
        replacement.Id.Should().NotBe(original.Id);
        replacement.Text.Should().Be("Новое назначение");
        replacement.ReplacesPrescriptionId.Should().Be(original.Id);
        replacement.CreatedAtUtc.Should().Be(replacedAt);
        record.Version.Should().Be(4);
        record.HistoryEvents.Select(x => x.Version).Should().Equal(1, 2, 3, 4);
        record.HistoryEvents.Skip(2).Select(x => x.EventType).Should().Equal(
            MedicalRecordEventType.PrescriptionCancelled, MedicalRecordEventType.PrescriptionAdded);
        record.HistoryEvents.Skip(2).Should().OnlyContain(x => x.OccurredAtUtc == replacedAt);
        record.HistoryEvents.ElementAt(2).PayloadJson.Should().Contain("prescriptionId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("аб")]
    [InlineData("  аб  ")]
    public void Invalid_replacement_does_not_cancel_original_or_append_history(string? text)
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var original = MedicalRecordService.AddPrescription(record, "Назначение", author, Now.AddMinutes(1));

        var replace = () => MedicalRecordService.ReplacePrescription(record, original.Id, text!, author, Now.AddMinutes(2));

        replace.Should().Throw<DomainValidationException>();
        original.CancelledAtUtc.Should().BeNull();
        original.CancelledByMedicalWorkerId.Should().BeNull();
        record.Prescriptions.Should().ContainSingle();
        record.Version.Should().Be(2);
        record.HistoryEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Prescription_boundaries_are_checked_before_changes()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        MedicalRecordService.AddPrescription(record, new string('а', 3), author, Now);
        var longPrescription = MedicalRecordService.AddPrescription(record, new string('а', 128), author, Now);
        var replace = () => MedicalRecordService.ReplacePrescription(record, longPrescription.Id, new string('а', 129), author, Now);
        replace.Should().Throw<DomainValidationException>();
        longPrescription.CancelledAtUtc.Should().BeNull();
        record.Version.Should().Be(3);
    }

    [Fact]
    public void Repeated_cancel_and_replace_of_cancelled_prescription_are_conflicts_without_new_history()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var prescription = MedicalRecordService.AddPrescription(record, "Назначение", author, Now);
        MedicalRecordService.CancelPrescription(record, prescription.Id, author, Now.AddMinutes(1));
        Action cancelAgain = () => MedicalRecordService.CancelPrescription(record, prescription.Id, author, Now.AddMinutes(2));
        var replace = () => MedicalRecordService.ReplacePrescription(record, prescription.Id, "Новое назначение", author, Now.AddMinutes(2));

        cancelAgain.Should().Throw<BusinessRuleConflictException>();
        replace.Should().Throw<BusinessRuleConflictException>();
        record.Version.Should().Be(3);
        record.Prescriptions.Should().ContainSingle();
    }

    [Fact]
    public void Diagnosis_removal_keeps_historical_data_and_repeated_removal_conflicts()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var definitionId = Guid.NewGuid();
        var diagnosis = MedicalRecordService.AddDiagnosis(record, definitionId, " I10.00 ", " Гипертензия ", author, Now.AddMinutes(1));
        MedicalRecordService.RemoveDiagnosis(record, diagnosis.Id, author, Now.AddMinutes(2));

        diagnosis.DiagnosisDefinitionId.Should().Be(definitionId);
        diagnosis.DiagnosisCodeSnapshot.Should().Be("I10.00");
        diagnosis.DiagnosisNameSnapshot.Should().Be("Гипертензия");
        diagnosis.AddedAtUtc.Should().Be(Now.AddMinutes(1));
        diagnosis.RemovedAtUtc.Should().Be(Now.AddMinutes(2));
        diagnosis.RemovedByMedicalWorkerId.Should().Be(author.Id);
        Action removeAgain = () => MedicalRecordService.RemoveDiagnosis(record, diagnosis.Id, author, Now.AddMinutes(3));
        removeAgain.Should().Throw<BusinessRuleConflictException>();
        record.Version.Should().Be(3);
        record.Diagnoses.Should().ContainSingle();
    }

    [Fact]
    public void Child_from_another_record_is_not_found_and_does_not_change_state()
    {
        var author = Author();
        var first = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var second = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var prescription = MedicalRecordService.AddPrescription(first, "Назначение", author, Now);
        var diagnosis = MedicalRecordService.AddDiagnosis(first, Guid.NewGuid(), "I10.00", "Гипертензия", author, Now);

        Action cancel = () => MedicalRecordService.CancelPrescription(second, prescription.Id, author, Now);
        Action remove = () => MedicalRecordService.RemoveDiagnosis(second, diagnosis.Id, author, Now);
        var replace = () => MedicalRecordService.ReplacePrescription(second, prescription.Id, "Изменение", author, Now);
        cancel.Should().Throw<DomainEntityNotFoundException>();
        remove.Should().Throw<DomainEntityNotFoundException>();
        replace.Should().Throw<DomainEntityNotFoundException>();
        second.Version.Should().Be(1);
        prescription.CancelledAtUtc.Should().BeNull();
    }

    [Fact]
    public void Deleted_author_cannot_create_or_change_medical_records()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var prescription = MedicalRecordService.AddPrescription(record, "Назначение", author, Now);
        var diagnosis = MedicalRecordService.AddDiagnosis(record, Guid.NewGuid(), "I10.00", "Гипертензия", author, Now);
        MedicalWorkerService.MarkAsDeleted(author, Now);
        var actions = new Action[]
        {
            () => MedicalRecordFactory.Create(Guid.NewGuid(), author, Now),
            () => MedicalRecordService.AddDiagnosis(record, Guid.NewGuid(), "I10.00", "Гипертензия", author, Now),
            () => MedicalRecordService.RemoveDiagnosis(record, diagnosis.Id, author, Now),
            () => MedicalRecordService.AddPrescription(record, "Назначение", author, Now),
            () => MedicalRecordService.CancelPrescription(record, prescription.Id, author, Now),
            () => MedicalRecordService.ReplacePrescription(record, prescription.Id, "Новое назначение", author, Now)
        };

        foreach (var action in actions) action.Should().Throw<DomainValidationException>();
        record.Version.Should().Be(3);
        prescription.CancelledAtUtc.Should().BeNull();
        diagnosis.RemovedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Clock_rollback_keeps_events_and_current_state_at_monotonic_utc_time()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var prescription = MedicalRecordService.AddPrescription(record, "Назначение", author, Now.AddMinutes(-1));
        MedicalRecordService.CancelPrescription(record, prescription.Id, author, Now.AddMinutes(-2));

        prescription.CreatedAtUtc.Should().Be(Now);
        prescription.CancelledAtUtc.Should().Be(Now);
        record.HistoryEvents.Should().OnlyContain(x => x.OccurredAtUtc == Now);
        record.HistoryEvents.Select(x => x.Version).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Unordered_restored_history_uses_latest_version_when_clock_moves_backwards()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var previous = MedicalRecordService.AddPrescription(record, "Первое назначение", author, Now.AddMinutes(5));
        record.HistoryEvents = record.HistoryEvents.Reverse().ToArray();

        var next = MedicalRecordService.AddPrescription(record, "Следующее назначение", author, Now.AddMinutes(1));

        next.CreatedAtUtc.Should().Be(previous.CreatedAtUtc);
        record.Version.Should().Be(3);
        record.HistoryEvents.Single(x => x.Version == 3).OccurredAtUtc.Should().Be(previous.CreatedAtUtc);
        var snapshot = HospitalInformationSystem.Application.MedicalRecords.Snapshot.MedicalRecordSnapshotReplay
            .Build(record, record.HistoryEvents, previous.CreatedAtUtc);
        snapshot.Prescriptions.Should().HaveCount(2);
    }

    [Fact]
    public void Current_dto_omits_removed_diagnoses_and_cancelled_prescriptions()
    {
        var author = Author();
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, Now);
        var removed = MedicalRecordService.AddDiagnosis(record, Guid.NewGuid(), "I10.00", "Гипертензия", author, Now);
        var active = MedicalRecordService.AddDiagnosis(record, Guid.NewGuid(), "E11.90", "Диабет", author, Now);
        MedicalRecordService.RemoveDiagnosis(record, removed.Id, author, Now);
        var cancelled = MedicalRecordService.AddPrescription(record, "Исходное назначение", author, Now);
        var replacement = MedicalRecordService.ReplacePrescription(record, cancelled.Id, "Новое назначение", author, Now);

        var dto = record.ToDto();

        dto.Version.Should().Be(record.Version);
        dto.Diagnoses.Should().ContainSingle(x => x.Id == active.Id && x.Code == "E11.90");
        dto.Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id && x.ReplacesPrescriptionId == cancelled.Id);
        record.Diagnoses.Should().HaveCount(2);
        record.Prescriptions.Should().HaveCount(2);
        var historyDto = record.HistoryEvents.First().ToDto();
        historyDto.EventType.Should().Be("MedicalRecordCreated");
        historyDto.PerformedByName.Should().Be("Иванов Алексей");
    }
}
