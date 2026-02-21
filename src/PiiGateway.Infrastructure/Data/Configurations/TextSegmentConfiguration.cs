using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class TextSegmentConfiguration : IEntityTypeConfiguration<TextSegment>
{
    public void Configure(EntityTypeBuilder<TextSegment> builder)
    {
        builder.ToTable("text_segments");

        builder.HasKey(ts => ts.Id);
        builder.Property(ts => ts.Id).HasColumnName("id");
        builder.Property(ts => ts.JobId).HasColumnName("job_id");
        builder.Property(ts => ts.SegmentIndex).HasColumnName("segment_index");
        builder.Property(ts => ts.TextContent).HasColumnName("text_content").IsRequired();
        builder.Property(ts => ts.SourceType).HasColumnName("source_type")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<SourceType>(v, true))
            .HasColumnType("varchar(50)");
        builder.Property(ts => ts.SourceLocation).HasColumnName("source_location").HasColumnType("jsonb");
        builder.Property(ts => ts.CreatedAt).HasColumnName("created_at");

        builder.HasOne(ts => ts.Job)
            .WithMany(j => j.TextSegments)
            .HasForeignKey(ts => ts.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
