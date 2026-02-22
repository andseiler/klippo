using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PiiGateway.Core.Domain.Entities;

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
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.RefreshToken).HasColumnName("refresh_token");
        builder.Property(u => u.RefreshTokenExpiryTime).HasColumnName("refresh_token_expiry_time");

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
