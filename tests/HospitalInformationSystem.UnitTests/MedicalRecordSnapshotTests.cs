using HospitalInformationSystem.Domain.MedicalRecords.Factories;
using HospitalInformationSystem.Domain.MedicalRecords.Services;
using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.MedicalRecords.Snapshot;
using HospitalInformationSystem.Domain.MedicalRecords;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Moq;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class MedicalRecordSnapshotTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(1);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(2);
    private static readonly DateTimeOffset T3 = T0.AddMinutes(3);
    private static readonly DateTimeOffset T4 = T0.AddMinutes(4);

    private static (MedicalRecord Record, MedicalRecordDiagnosis Diagnosis, Prescription Original, Prescription
        Replacement) Scenario()
    {
        var author = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", T0);
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, T0);
        var diagnosis = MedicalRecordService.AddDiagnosis(record, Guid.NewGuid(), "I10.00", "Гипертензия", author, T1);
        var original = MedicalRecordService.AddPrescription(record, "Исходное назначение", author, T2);
        MedicalRecordService.RemoveDiagnosis(record, diagnosis.Id, author, T3);
        var replacement = MedicalRecordService.ReplacePrescription(record, original.Id, "Новое назначение", author, T4);
        return (record, diagnosis, original, replacement);
    }

    [Fact]
    public void Replay_reconstructs_each_step_T0_to_T4_at_inclusive_boundaries()
    {
        var (record, diagnosis, original, replacement) = Scenario();

        var created = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T0);
        created.Version.Should().Be(1);
        created.Diagnoses.Should().BeEmpty();
        created.Prescriptions.Should().BeEmpty();

        var diagnosed = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T1);
        diagnosed.Version.Should().Be(2);
        diagnosed.Diagnoses.Should()
            .ContainSingle(x => x.Id == diagnosis.Id && x.Code == "I10.00" && x.Name == "Гипертензия");
        diagnosed.Prescriptions.Should().BeEmpty();

        var prescribed = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T2);
        prescribed.Version.Should().Be(3);
        prescribed.Diagnoses.Should().ContainSingle();
        prescribed.Prescriptions.Should().ContainSingle(x => x.Id == original.Id && x.Text == "Исходное назначение");

        var removed = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T3);
        removed.Version.Should().Be(4);
        removed.Diagnoses.Should().BeEmpty();
        removed.Prescriptions.Should().ContainSingle(x => x.Id == original.Id);

        var replaced = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T4);
        replaced.Version.Should().Be(6);
        replaced.Diagnoses.Should().BeEmpty();
        replaced.Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id && x.Text == "Новое назначение"
            && x.ReplacesPrescriptionId == original.Id);
    }

    [Fact]
    public void Before_event_uses_previous_state_and_after_last_event_uses_final_state()
    {
        var (record, _, original, replacement) = Scenario();
        var justBefore = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T4.AddTicks(-1));
        justBefore.Version.Should().Be(4);
        justBefore.Prescriptions.Should().ContainSingle(x => x.Id == original.Id);

        var future = T4.AddYears(10).ToOffset(TimeSpan.FromHours(3));
        var after = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, future);
        after.Version.Should().Be(6);
        after.Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id);
        after.At.Should().Be(future);
        after.At.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Before_creation_is_not_found_instead_of_an_empty_record()
    {
        var (record, _, _, _) = Scenario();
        var snapshot = () => MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents, T0.AddTicks(-1));
        snapshot.Should().Throw<EntityNotFoundException>();
    }

    [Fact]
    public void Equal_timestamps_are_replayed_by_version_not_input_order()
    {
        var author = MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач", T0);
        var record = MedicalRecordFactory.Create(Guid.NewGuid(), author, T0);
        var original = MedicalRecordService.AddPrescription(record, "Исходное назначение", author, T0);
        var replacement = MedicalRecordService.ReplacePrescription(record, original.Id, "Новое назначение", author, T0);

        var snapshot = MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents.Reverse(), T0);

        snapshot.Version.Should().Be(4);
        snapshot.Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id);
    }

    [Fact]
    public void Replay_uses_payload_even_if_current_state_and_catalog_reference_are_missing()
    {
        var (record, diagnosis, original, _) = Scenario();
        var withoutCurrentState = new MedicalRecord
        {
            Id = record.Id, PatientId = record.PatientId, CreatedAtUtc = record.CreatedAtUtc,
            Version = record.Version, HistoryEvents = record.HistoryEvents
        };

        var snapshot = MedicalRecordSnapshotReplay.Build(withoutCurrentState, withoutCurrentState.HistoryEvents, T2);

        snapshot.Diagnoses.Should().ContainSingle(x => x.Id == diagnosis.Id && x.Name == "Гипертензия"
                                                                            && x.AddedByMedicalWorkerId ==
                                                                            diagnosis.AddedByMedicalWorkerId &&
                                                                            x.AddedAtUtc == T1);
        snapshot.Prescriptions.Should().ContainSingle(x => x.Id == original.Id && x.Text == "Исходное назначение"
                                                                               && x.CreatedByMedicalWorkerId ==
                                                                               original.CreatedByMedicalWorkerId &&
                                                                               x.CreatedAtUtc == T2);
    }

    [Fact]
    public void Missing_versions_and_unknown_event_types_are_rejected()
    {
        var (record, _, _, _) = Scenario();
        var missingVersion = () =>
            MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents.Where(x => x.Version != 2), T4);
        missingVersion.Should().Throw<InvalidOperationException>().WithMessage("*последовательность версий*");

        var unknown = ReplaceEvent(record.HistoryEvents.ElementAt(1), type: (MedicalRecordEventType)999);
        var unknownEvent = () => MedicalRecordSnapshotReplay.Build(record, [record.HistoryEvents.First(), unknown], T1);
        unknownEvent.Should().Throw<InvalidOperationException>().WithMessage("*неизвестный тип*");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Corrupt_or_incomplete_payload_is_rejected(string payload)
    {
        var (record, _, _, _) = Scenario();
        var corrupt = ReplaceEvent(record.HistoryEvents.ElementAt(1), payload: payload);
        var snapshot = () => MedicalRecordSnapshotReplay.Build(record, [record.HistoryEvents.First(), corrupt], T1);
        snapshot.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Other_record_events_are_rejected_instead_of_polluting_snapshot()
    {
        var (record, _, _, _) = Scenario();
        var other = ReplaceEvent(record.HistoryEvents.First(), recordId: Guid.NewGuid());
        var snapshot = () => MedicalRecordSnapshotReplay.Build(record, [other], T0);
        snapshot.Should().Throw<InvalidOperationException>().WithMessage("*другой карте*");
    }

    [Fact]
    public void Missing_tail_of_history_is_rejected_instead_of_returning_an_outdated_snapshot()
    {
        var (record, _, _, _) = Scenario();
        var snapshot = () => MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents.SkipLast(1), T4);

        snapshot.Should().Throw<InvalidOperationException>().WithMessage("*верси*карты*");
    }

    [Fact]
    public void Historical_snapshot_also_rejects_a_gap_in_later_history()
    {
        var (record, _, _, _) = Scenario();
        var snapshot = () =>
            MedicalRecordSnapshotReplay.Build(record, record.HistoryEvents.Where(x => x.Version != 5), T1);

        snapshot.Should().Throw<InvalidOperationException>().WithMessage("*последовательность версий*");
    }

    [Fact]
    public async Task Builder_returns_not_found_for_missing_record()
    {
        var repository = new Mock<IMedicalRecordRepository>();
        var builder = new MedicalRecordSnapshotBuilder(repository.Object);
        var snapshot = () => builder.BuildAsync(Guid.NewGuid(), T0, CancellationToken.None);
        await snapshot.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Builder_reads_without_tracking_and_returns_replayed_state()
    {
        var (record, _, original, _) = Scenario();
        var repository = new Mock<IMedicalRecordRepository>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        repository.Setup(x => x.FindAsync(record.Id, false, cancellation.Token)).ReturnsAsync(record);
        var builder = new MedicalRecordSnapshotBuilder(repository.Object);

        var snapshot = await builder.BuildAsync(record.Id, T2, cancellation.Token);

        snapshot.MedicalRecordId.Should().Be(record.Id);
        snapshot.PatientId.Should().Be(record.PatientId);
        snapshot.Version.Should().Be(3);
        snapshot.Prescriptions.Should().ContainSingle(x => x.Id == original.Id);
        repository.VerifyAll();
    }

    private static MedicalRecordHistoryEvent ReplaceEvent(
        MedicalRecordHistoryEvent item, MedicalRecordEventType? type = null, string? payload = null,
        Guid? recordId = null) =>
        new()
        {
            Id = item.Id, MedicalRecordId = recordId ?? item.MedicalRecordId, Version = item.Version,
            OccurredAtUtc = item.OccurredAtUtc, PerformedByMedicalWorkerId = item.PerformedByMedicalWorkerId,
            PerformedByNameSnapshot = item.PerformedByNameSnapshot,
            PerformedByPositionSnapshot = item.PerformedByPositionSnapshot,
            EventType = type ?? item.EventType, EntityId = item.EntityId, PayloadJson = payload ?? item.PayloadJson
        };
}
