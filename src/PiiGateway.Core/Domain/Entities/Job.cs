using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedById { get; set; }
    public JobStatus Status { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public long FileSizeBytes { get; set; }
    public bool SecondScanPassed { get; set; }
    public bool ExportAcknowledged { get; set; }
    public bool IsGuest { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public DateTime? PseudonymizedAt { get; set; }
    public string? PseudonymizedText { get; set; }

    public Organization Organization { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<TextSegment> TextSegments { get; set; } = new List<TextSegment>();
    public ICollection<PiiEntity> PiiEntities { get; set; } = new List<PiiEntity>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
