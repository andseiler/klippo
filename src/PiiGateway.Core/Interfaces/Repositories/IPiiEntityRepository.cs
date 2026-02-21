using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Interfaces.Repositories;

public interface IPiiEntityRepository
{
    Task<PiiEntity?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<PiiEntity>> GetByJobIdAsync(Guid jobId);
    Task<IReadOnlyList<PiiEntity>> GetByJobIdAndStatusAsync(Guid jobId, ReviewStatus status);
    Task AddRangeAsync(IEnumerable<PiiEntity> entities);
    Task UpdateAsync(PiiEntity entity);
    Task UpdateRangeAsync(IEnumerable<PiiEntity> entities);
    Task DeleteAsync(PiiEntity entity);
    Task DeleteRangeAsync(IEnumerable<PiiEntity> entities);
    Task<int> CountByJobIdAsync(Guid jobId);
    Task<Dictionary<ReviewStatus, int>> GetStatusCountsAsync(Guid jobId);
}
