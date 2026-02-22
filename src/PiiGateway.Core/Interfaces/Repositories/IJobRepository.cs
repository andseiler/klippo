using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Interfaces.Repositories;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize);
    Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByUserFilteredAsync(
        Guid userId, int page, int pageSize,
        JobStatus? statusFilter,
        DateTime? dateFrom, DateTime? dateTo);
    Task<Job> CreateAsync(Job job);
    Task UpdateAsync(Job job);
    Task DeleteAsync(Job job);
    Task<IReadOnlyList<Job>> GetExpiredGuestJobsAsync(DateTime cutoff);
    Task<IReadOnlyList<Job>> GetTerminalJobsOlderThanAsync(DateTime cutoff);
}
