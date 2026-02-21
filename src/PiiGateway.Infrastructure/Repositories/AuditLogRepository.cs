using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly PiiGatewayDbContext _context;

    public AuditLogRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AuditLog>> GetByJobIdAsync(Guid jobId)
    {
        return await _context.AuditLogs
            .Where(al => al.JobId == jobId)
            .OrderByDescending(al => al.Timestamp)
            .ToListAsync();
    }

    public async Task DeleteByJobIdAsync(Guid jobId)
    {
        var logs = await _context.AuditLogs.Where(al => al.JobId == jobId).ToListAsync();
        _context.AuditLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();
    }
}
