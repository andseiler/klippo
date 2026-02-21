using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class TextSegmentRepository : ITextSegmentRepository
{
    private readonly PiiGatewayDbContext _context;

    public TextSegmentRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<TextSegment> segments)
    {
        _context.TextSegments.AddRange(segments);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TextSegment>> GetByJobIdAsync(Guid jobId)
    {
        return await _context.TextSegments
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.SegmentIndex)
            .ToListAsync();
    }

    public async Task UpdateAsync(TextSegment segment)
    {
        _context.TextSegments.Update(segment);
        await _context.SaveChangesAsync();
    }
}
