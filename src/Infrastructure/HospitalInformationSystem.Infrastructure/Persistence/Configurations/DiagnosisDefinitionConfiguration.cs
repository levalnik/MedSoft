using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class DiagnosisDefinitionConfiguration : IEntityTypeConfiguration<DiagnosisDefinitionDbo>
{
    public void Configure(EntityTypeBuilder<DiagnosisDefinitionDbo> builder)
    {
        builder.ToTable("diagnosis_definitions", table =>
        {
            table.HasCheckConstraint("ck_diagnosis_definitions_code", "char_length(btrim(code)) = 6");
            table.HasCheckConstraint("ck_diagnosis_definitions_name", "char_length(btrim(name)) BETWEEN 3 AND 24");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(6).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_diagnosis_definitions_code");
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_diagnosis_definitions_name");
    }
}

