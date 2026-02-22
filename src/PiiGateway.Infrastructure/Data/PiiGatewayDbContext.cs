using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Infrastructure.Data;

public class PiiGatewayDbContext : DbContext
{
    public PiiGatewayDbContext(DbContextOptions<PiiGatewayDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<TextSegment> TextSegments => Set<TextSegment>();
    public DbSet<PiiEntity> PiiEntities => Set<PiiEntity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PiiGatewayDbContext).Assembly);
    }
}
