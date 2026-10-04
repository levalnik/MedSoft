using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class MedicalRecordDiagnosisConfiguration : IEntityTypeConfiguration<MedicalRecordDiagnosisDbo>
{
    public void Configure(EntityTypeBuilder<MedicalRecordDiagnosisDbo> builder)
    {
        builder.ToTable("medical_record_diagnoses", table =>
        {
            table.HasCheckConstraint("ck_record_diagnoses_code", "char_length(diagnosis_code_snapshot) = 6");
            table.HasCheckConstraint("ck_record_diagnoses_name", "char_length(btrim(diagnosis_name_snapshot)) BETWEEN 3 AND 24");
            table.HasCheckConstraint("ck_record_diagnoses_removal", "(removed_at_utc IS NULL) = (removed_by_medical_worker_id IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnName("medical_record_id");
        builder.Property(x => x.DiagnosisDefinitionId).HasColumnName("diagnosis_definition_id");
        builder.Property(x => x.DiagnosisCodeSnapshot).HasColumnName("diagnosis_code_snapshot").HasMaxLength(6).IsRequired();
        builder.Property(x => x.DiagnosisNameSnapshot).HasColumnName("diagnosis_name_snapshot").HasMaxLength(24).IsRequired();
        builder.Property(x => x.AddedAtUtc).HasColumnName("added_at_utc");
        builder.Property(x => x.AddedByMedicalWorkerId).HasColumnName("added_by_medical_worker_id");
        builder.Property(x => x.RemovedAtUtc).HasColumnName("removed_at_utc");
        builder.Property(x => x.RemovedByMedicalWorkerId).HasColumnName("removed_by_medical_worker_id");
        builder.HasOne<DiagnosisDefinitionDbo>().WithMany().HasForeignKey(x => x.DiagnosisDefinitionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.AddedByMedicalWorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.RemovedByMedicalWorkerId).OnDelete(DeleteBehavior.Restrict);
    }
}
