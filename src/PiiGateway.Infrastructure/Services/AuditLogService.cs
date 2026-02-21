using System.Security.Cryptography;
using System.Text;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;

namespace PiiGateway.Infrastructure.Services;

public class AuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task LogAsync(
        Guid jobId,
        ActionType actionType,
        Guid? actorId = null,
        string? entityType = null,
        string? rawPiiText = null,
        double? confidence = null,
        string? detectionSource = null,
        string? metadata = null,
        string? ipAddress = null)
    {
        var auditLog = new AuditLog
        {
            JobId = jobId,
            Timestamp = DateTime.UtcNow,
            ActorId = actorId,
            ActionType = actionType,
            EntityType = entityType,
            EntityHash = rawPiiText != null ? HashWithSha256(rawPiiText) : null,
            Confidence = confidence,
            DetectionSource = detectionSource,
            Metadata = metadata,
            IpAddress = ipAddress
        };

        await _auditLogRepository.AppendAsync(auditLog);
    }

    private static string HashWithSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
