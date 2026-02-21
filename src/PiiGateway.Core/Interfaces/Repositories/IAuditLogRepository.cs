using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Core.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditLog auditLog);
    Task<IReadOnlyList<AuditLog>> GetByJobIdAsync(Guid jobId);
    Task DeleteByJobIdAsync(Guid jobId);
}
