using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class PiiEntityRepository : IPiiEntityRepository
{
    private readonly PiiGatewayDbContext _context;

    public PiiEntityRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<PiiEntity?> GetByIdAsync(Guid id)
    {
        return await _context.PiiEntities
            .Include(e => e.Segment)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IReadOnlyList<PiiEntity>> GetByJobIdAsync(Guid jobId)
    {
        return await _context.PiiEntities
            .Include(e => e.Segment)
            .Where(e => e.JobId == jobId)
            .OrderBy(e => e.Segment.SegmentIndex)
            .ThenBy(e => e.StartOffset)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PiiEntity>> GetByJobIdAndStatusAsync(Guid jobId, ReviewStatus status)
    {
        return await _context.PiiEntities
            .Include(e => e.Segment)
            .Where(e => e.JobId == jobId && e.ReviewStatus == status)
            .OrderBy(e => e.Segment.SegmentIndex)
            .ThenBy(e => e.StartOffset)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<PiiEntity> entities)
    {
        _context.PiiEntities.AddRange(entities);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PiiEntity entity)
    {
        _context.PiiEntities.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<PiiEntity> entities)
    {
        _context.PiiEntities.UpdateRange(entities);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(PiiEntity entity)
    {
        _context.PiiEntities.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteRangeAsync(IEnumerable<PiiEntity> entities)
    {
        _context.PiiEntities.RemoveRange(entities);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountByJobIdAsync(Guid jobId)
    {
        return await _context.PiiEntities
            .Where(e => e.JobId == jobId)
            .CountAsync();
    }

    public async Task<Dictionary<ReviewStatus, int>> GetStatusCountsAsync(Guid jobId)
    {
        return await _context.PiiEntities
            .Where(e => e.JobId == jobId)
            .GroupBy(e => e.ReviewStatus)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }
}
