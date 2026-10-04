using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class MedicalRecordHistoryEventConfiguration : IEntityTypeConfiguration<MedicalRecordHistoryEventDbo>
{
    public void Configure(EntityTypeBuilder<MedicalRecordHistoryEventDbo> builder)
    {
        builder.ToTable("medical_record_history_events", table =>
        {
            table.HasCheckConstraint("ck_record_history_version", "version > 0");
            table.HasCheckConstraint("ck_record_history_type", "event_type IN ('MedicalRecordCreated', 'DiagnosisAdded', 'DiagnosisRemoved', 'PrescriptionAdded', 'PrescriptionCancelled')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnName("medical_record_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.PerformedByMedicalWorkerId).HasColumnName("performed_by_medical_worker_id");
        builder.Property(x => x.PerformedByNameSnapshot).HasColumnName("performed_by_name_snapshot").HasMaxLength(49).IsRequired();
        builder.Property(x => x.PerformedByPositionSnapshot).HasColumnName("performed_by_position_snapshot").HasMaxLength(24).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.PerformedByMedicalWorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MedicalRecordId, x.Version }).IsUnique().HasDatabaseName("ux_record_history_version");
    }
}
