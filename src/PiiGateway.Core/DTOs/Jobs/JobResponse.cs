namespace PiiGateway.Core.DTOs.Jobs;

public class JobResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedById { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public long FileSizeBytes { get; set; }
    public bool SecondScanPassed { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public DateTime? PseudonymizedAt { get; set; }
    public int? TotalEntities { get; set; }
    public int? ConfirmedEntities { get; set; }
    public int? ManualEntities { get; set; }
    public int? PendingEntities { get; set; }
}
