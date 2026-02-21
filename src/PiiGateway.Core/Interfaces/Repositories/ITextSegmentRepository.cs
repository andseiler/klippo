using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Core.Interfaces.Repositories;

public interface ITextSegmentRepository
{
    Task AddRangeAsync(IEnumerable<TextSegment> segments);
    Task<IReadOnlyList<TextSegment>> GetByJobIdAsync(Guid jobId);
    Task UpdateAsync(TextSegment segment);
}
