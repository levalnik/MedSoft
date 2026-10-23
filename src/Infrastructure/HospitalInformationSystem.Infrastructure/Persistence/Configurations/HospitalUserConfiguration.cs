using HospitalInformationSystem.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class HospitalUserConfiguration : IEntityTypeConfiguration<HospitalUser>
{
    public void Configure(EntityTypeBuilder<HospitalUser> builder)
    {
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("ck_users_username", "\"UserName\" IS NOT NULL AND \"UserName\" ~ '^[A-Za-z0-9._@+-]{3,16}$'");
            table.HasCheckConstraint("ck_users_normalized_username", "\"NormalizedUserName\" IS NOT NULL AND \"NormalizedUserName\" = upper(\"UserName\")");
            table.HasCheckConstraint("ck_users_admin_worker", "NOT \"IsAdministrator\" OR \"MedicalWorkerId\" IS NULL");
            table.HasCheckConstraint("ck_users_dates", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.Property(x => x.UserName).HasMaxLength(16).IsRequired();
        builder.Property(x => x.NormalizedUserName).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.IsAdministrator).HasDefaultValue(false);
        builder.HasIndex(x => x.IsAdministrator).IsUnique().HasDatabaseName("IX_AspNetUsers_SingleAdministrator")
            .HasFilter("\"IsAdministrator\" = TRUE");
    }
}
