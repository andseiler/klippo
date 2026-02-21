namespace PiiGateway.Core.DTOs.Audit;

public class AuditLogResponse
{
    public List<AuditEntryDto> Entries { get; set; } = new();
}

public class AuditEntryDto
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? ActorId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityHash { get; set; }
    public double? Confidence { get; set; }
    public string? DetectionSource { get; set; }
    public string? Metadata { get; set; }
}
