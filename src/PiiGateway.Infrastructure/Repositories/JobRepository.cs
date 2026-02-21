using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly PiiGatewayDbContext _context;

    public JobRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<Job?> GetByIdAsync(Guid id)
    {
        return await _context.Jobs
            .Include(j => j.CreatedBy)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByOrgAsync(Guid organizationId, int page, int pageSize)
    {
        var query = _context.Jobs
            .Where(j => j.OrganizationId == organizationId && !j.IsGuest)
            .OrderByDescending(j => j.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByOrgFilteredAsync(
        Guid organizationId, int page, int pageSize,
        JobStatus? statusFilter,
        DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _context.Jobs
            .Where(j => j.OrganizationId == organizationId && !j.IsGuest);

        if (statusFilter.HasValue)
            query = query.Where(j => j.Status == statusFilter.Value);

        if (dateFrom.HasValue)
            query = query.Where(j => j.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(j => j.CreatedAt <= dateTo.Value);

        var orderedQuery = query.OrderByDescending(j => j.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Job> CreateAsync(Job job)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateAsync(Job job)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Job job)
    {
        _context.Jobs.Remove(job);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Job>> GetExpiredGuestJobsAsync(DateTime cutoff)
    {
        return await _context.Jobs
            .Where(j => j.IsGuest && j.CreatedAt < cutoff)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Job>> GetTerminalJobsOlderThanAsync(DateTime cutoff)
    {
        // Terminal statuses: DePseudonymized, ScanPassed, ScanFailed, Failed
        var terminalStatuses = new[]
        {
            JobStatus.DePseudonymized,
            JobStatus.ScanPassed,
            JobStatus.ScanFailed,
            JobStatus.Failed
        };

        return await _context.Jobs
            .Where(j => terminalStatuses.Contains(j.Status) && j.CreatedAt < cutoff)
            .ToListAsync();
    }
}
