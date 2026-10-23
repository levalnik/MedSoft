using HospitalInformationSystem.Infrastructure.Identity;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class AdministratorSetupStateConfiguration : IEntityTypeConfiguration<AdministratorSetupStateDbo>
{
    public void Configure(EntityTypeBuilder<AdministratorSetupStateDbo> builder)
    {
        builder.ToTable("administrator_setup_state", table =>
        {
            table.HasCheckConstraint("ck_setup_singleton", "id = 1");
            table.HasCheckConstraint("ck_setup_completion", "NOT is_completed OR (administrator_user_id IS NOT NULL AND totp_secret_protected IS NOT NULL AND completed_at_utc IS NOT NULL)");
            table.HasCheckConstraint("ck_setup_dates", "updated_at_utc >= created_at_utc AND (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AdministratorUserId).HasColumnName("administrator_user_id");
        builder.Property(x => x.IsCompleted).HasColumnName("is_completed");
        builder.Property(x => x.TotpSecretProtected).HasColumnName("totp_secret_protected");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.HasOne<HospitalUser>().WithMany().HasForeignKey(x => x.AdministratorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
