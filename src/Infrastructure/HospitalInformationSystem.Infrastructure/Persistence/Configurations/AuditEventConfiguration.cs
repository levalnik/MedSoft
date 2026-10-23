using HospitalInformationSystem.Infrastructure.Identity;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalInformationSystem.Infrastructure.Persistence.Configurations;

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
