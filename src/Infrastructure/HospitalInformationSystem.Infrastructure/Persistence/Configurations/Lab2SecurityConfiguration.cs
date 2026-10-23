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

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventDbo>
{
    public void Configure(EntityTypeBuilder<AuditEventDbo> builder)
    {
        builder.ToTable("security_audit_events", table =>
            table.HasCheckConstraint("ck_audit_actor", "actor_kind IN ('user', 'anonymous', 'system') AND ((actor_kind = 'user' AND actor_user_id IS NOT NULL) OR (actor_kind <> 'user' AND actor_user_id IS NULL))"));
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(x => x.EventTimeUtc).HasColumnName("event_time_utc");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorKind).HasColumnName("actor_kind").HasMaxLength(16).IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.ActorDisplayName).HasColumnName("actor_display_name").HasMaxLength(256).IsRequired();
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasMaxLength(128);
        builder.Property(x => x.Component).HasColumnName("component").HasMaxLength(128);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.EventTimeUtc, x.EventId });
        builder.HasIndex(x => new { x.EventType, x.EventTimeUtc, x.EventId });
    }
}

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
