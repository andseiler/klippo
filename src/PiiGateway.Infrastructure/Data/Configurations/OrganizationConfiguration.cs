using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
        builder.Property(o => o.Plan).HasColumnName("plan").HasMaxLength(50);
        builder.Property(o => o.LlmProvider).HasColumnName("llm_provider").HasMaxLength(50);
        builder.Property(o => o.Settings).HasColumnName("settings").HasColumnType("jsonb");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
    }
}
