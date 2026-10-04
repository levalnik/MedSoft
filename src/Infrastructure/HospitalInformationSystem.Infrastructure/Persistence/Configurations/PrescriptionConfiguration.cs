using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<PrescriptionDbo>
{
    public void Configure(EntityTypeBuilder<PrescriptionDbo> builder)
    {
        builder.ToTable("prescriptions", table =>
        {
            table.HasCheckConstraint("ck_prescriptions_text", "char_length(btrim(text)) BETWEEN 3 AND 128");
            table.HasCheckConstraint("ck_prescriptions_cancellation", "(cancelled_at_utc IS NULL) = (cancelled_by_medical_worker_id IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnName("medical_record_id");
        builder.Property(x => x.Text).HasColumnName("text").HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.CreatedByMedicalWorkerId).HasColumnName("created_by_medical_worker_id");
        builder.Property(x => x.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(x => x.CancelledByMedicalWorkerId).HasColumnName("cancelled_by_medical_worker_id");
        builder.Property(x => x.ReplacesPrescriptionId).HasColumnName("replaces_prescription_id");
        builder.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.CreatedByMedicalWorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.CancelledByMedicalWorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PrescriptionDbo>().WithMany().HasForeignKey(x => x.ReplacesPrescriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ReplacesPrescriptionId).IsUnique();
    }
}
