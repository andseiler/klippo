using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Infrastructure.Data.Converters;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class PiiEntityConfiguration : IEntityTypeConfiguration<PiiEntity>
{
    public void Configure(EntityTypeBuilder<PiiEntity> builder)
    {
        builder.ToTable("pii_entities");

        builder.HasKey(pe => pe.Id);
        builder.Property(pe => pe.Id).HasColumnName("id");
        builder.Property(pe => pe.JobId).HasColumnName("job_id");
        builder.Property(pe => pe.SegmentId).HasColumnName("segment_id");
        builder.Property(pe => pe.OriginalTextEnc).HasColumnName("original_text_enc")
            .HasConversion(EncryptedStringConverter.Instance);
        builder.Property(pe => pe.ReplacementText).HasColumnName("replacement_text");
        builder.Property(pe => pe.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(100);
        builder.Property(pe => pe.StartOffset).HasColumnName("start_offset");
        builder.Property(pe => pe.EndOffset).HasColumnName("end_offset");
        builder.Property(pe => pe.Confidence).HasColumnName("confidence");
        builder.Property(pe => pe.DetectionSources).HasColumnName("detection_sources").HasColumnType("text[]");
        builder.Property(pe => pe.ReviewStatus).HasColumnName("review_status")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<ReviewStatus>(v, true))
            .HasColumnType("varchar(50)");
        builder.Property(pe => pe.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(pe => pe.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(pe => pe.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(pe => new { pe.JobId, pe.ReviewStatus }).HasDatabaseName("idx_pii_job_status");

        builder.HasOne(pe => pe.Job)
            .WithMany(j => j.PiiEntities)
            .HasForeignKey(pe => pe.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pe => pe.Segment)
            .WithMany()
            .HasForeignKey(pe => pe.SegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pe => pe.ReviewedBy)
            .WithMany()
            .HasForeignKey(pe => pe.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
