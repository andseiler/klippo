using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");
        builder.Property(j => j.CreatedById).HasColumnName("created_by_id");
        builder.Property(j => j.Status).HasColumnName("status")
            .HasConversion(v => v.ToString().ToLower(), v => v == "exported" ? JobStatus.Pseudonymized : Enum.Parse<JobStatus>(v, true))
            .HasColumnType("varchar(50)");
        builder.Property(j => j.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(500);
        builder.Property(j => j.FileType).HasColumnName("file_type").IsRequired().HasMaxLength(50);
        builder.Property(j => j.FileHash).HasColumnName("file_hash").HasMaxLength(128);
        builder.Property(j => j.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(j => j.SecondScanPassed).HasColumnName("second_scan_passed");
        builder.Property(j => j.ExportAcknowledged).HasColumnName("export_acknowledged");
        builder.Property(j => j.IsGuest).HasColumnName("is_guest").HasDefaultValue(false);
        builder.Property(j => j.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(j => j.CreatedAt).HasColumnName("created_at");
        builder.Property(j => j.ProcessingStartedAt).HasColumnName("processing_started_at");
        builder.Property(j => j.ReviewStartedAt).HasColumnName("review_started_at");
        builder.Property(j => j.PseudonymizedAt).HasColumnName("pseudonymized_at");
        builder.Property(j => j.PseudonymizedText).HasColumnName("pseudonymized_text");

        builder.HasIndex(j => new { j.CreatedById, j.Status }).HasDatabaseName("idx_jobs_user_status");

        builder.HasOne(j => j.CreatedBy)
            .WithMany()
            .HasForeignKey(j => j.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
