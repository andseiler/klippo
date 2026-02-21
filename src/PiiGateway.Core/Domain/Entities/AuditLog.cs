using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? ActorId { get; set; }
    public ActionType ActionType { get; set; }
    public string? EntityType { get; set; }
    public string? EntityHash { get; set; }
    public double? Confidence { get; set; }
    public string? DetectionSource { get; set; }
    public string? Metadata { get; set; }
    public string? IpAddress { get; set; }

    public Job Job { get; set; } = null!;
    public User? Actor { get; set; }
}
