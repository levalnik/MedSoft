using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<PatientDbo>
{
    public void Configure(EntityTypeBuilder<PatientDbo> builder)
    {
        builder.ToTable("patients", table =>
        {
            table.HasCheckConstraint("ck_patients_first_name", "char_length(btrim(first_name)) BETWEEN 1 AND 64");
            table.HasCheckConstraint("ck_patients_last_name", "char_length(btrim(last_name)) BETWEEN 1 AND 64");
            table.HasCheckConstraint("ck_patients_middle_name",
                "middle_name IS NULL OR char_length(btrim(middle_name)) BETWEEN 1 AND 64");
            table.HasCheckConstraint("ck_patients_comment", "comment IS NULL OR char_length(comment) <= 32");
            table.HasCheckConstraint("ck_patients_sex", "sex IN (0, 1, 2)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.MiddleName).HasColumnName("middle_name").HasMaxLength(64);
        builder.Property(x => x.BirthDate).HasColumnName("birth_date");
        builder.Property(x => x.Sex).HasColumnName("sex");
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(32);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
