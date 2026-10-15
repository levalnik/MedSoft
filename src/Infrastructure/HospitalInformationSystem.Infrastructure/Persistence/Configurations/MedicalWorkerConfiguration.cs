using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class MedicalWorkerConfiguration : IEntityTypeConfiguration<MedicalWorkerDbo>
{
    public void Configure(EntityTypeBuilder<MedicalWorkerDbo> builder)
    {
        builder.ToTable("medical_workers", table =>
        {
            table.HasCheckConstraint("ck_medical_workers_first_name",
                "char_length(btrim(first_name)) BETWEEN 3 AND 24");
            table.HasCheckConstraint("ck_medical_workers_last_name", "char_length(btrim(last_name)) BETWEEN 3 AND 24");
            table.HasCheckConstraint("ck_medical_workers_position", "char_length(btrim(position)) BETWEEN 3 AND 24");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(24).IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Position).HasColumnName("position").HasMaxLength(24).IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
