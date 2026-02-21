using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
        builder.Property(u => u.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.OrganizationId).HasColumnName("organization_id");
        builder.Property(u => u.Role).HasColumnName("role")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<UserRole>(v, true))
            .HasColumnType("varchar(50)");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.RefreshToken).HasColumnName("refresh_token");
        builder.Property(u => u.RefreshTokenExpiryTime).HasColumnName("refresh_token_expiry_time");

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Organization)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
