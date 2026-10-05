using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecordDbo>
{
    public void Configure(EntityTypeBuilder<MedicalRecordDbo> builder)
    {
        builder.ToTable("medical_records",
            table => table.HasCheckConstraint("ck_medical_records_version", "version > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PatientId).HasColumnName("patient_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.PatientId).IsUnique().HasDatabaseName("ux_medical_records_patient_id");
        builder.HasOne<PatientDbo>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Diagnoses).WithOne().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Prescriptions).WithOne().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.HistoryEvents).WithOne().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
