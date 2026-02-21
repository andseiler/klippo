using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(al => al.Id);
        builder.Property(al => al.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(al => al.JobId).HasColumnName("job_id");
        builder.Property(al => al.Timestamp).HasColumnName("timestamp");
        builder.Property(al => al.ActorId).HasColumnName("actor_id");
        builder.Property(al => al.ActionType).HasColumnName("action_type")
            .HasConversion(v => v.ToString().ToLower(), v => v == "exportacknowledged" ? ActionType.JobStatusChanged : Enum.Parse<ActionType>(v, true))
            .HasColumnType("varchar(50)");
        builder.Property(al => al.EntityType).HasColumnName("entity_type").HasMaxLength(100);
        builder.Property(al => al.EntityHash).HasColumnName("entity_hash").HasMaxLength(64);
        builder.Property(al => al.Confidence).HasColumnName("confidence");
        builder.Property(al => al.DetectionSource).HasColumnName("detection_source").HasMaxLength(100);
        builder.Property(al => al.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(al => al.IpAddress).HasColumnName("ip_address").HasMaxLength(45);

        builder.HasIndex(al => al.JobId).HasDatabaseName("idx_audit_job");

        builder.HasOne(al => al.Job)
            .WithMany(j => j.AuditLogs)
            .HasForeignKey(al => al.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(al => al.Actor)
            .WithMany()
            .HasForeignKey(al => al.ActorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
