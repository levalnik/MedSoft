using HospitalInformationSystem.Infrastructure.Identity;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSessionDbo>
{
    public void Configure(EntityTypeBuilder<RefreshSessionDbo> builder)
    {
        builder.ToTable("refresh_sessions", table =>
        {
            table.HasCheckConstraint("ck_refresh_dates", "expires_at_utc > created_at_utc AND (revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc)");
            table.HasCheckConstraint("ck_refresh_hash", "token_hash ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_refresh_replacement", "replaced_by_session_id IS NULL OR (revoked_at_utc IS NOT NULL AND replaced_by_session_id <> id)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.FamilyId).HasColumnName("family_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(x => x.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
        builder.Property(x => x.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(64);
        builder.HasOne<HospitalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshSessionDbo>().WithMany().HasForeignKey(x => x.ReplacedBySessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
        builder.HasIndex(x => x.FamilyId);
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
