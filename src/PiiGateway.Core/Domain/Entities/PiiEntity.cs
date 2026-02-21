using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain.Entities;

public class PiiEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid SegmentId { get; set; }
    public string? OriginalTextEnc { get; set; }
    public string? ReplacementText { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public double Confidence { get; set; }
    public string[] DetectionSources { get; set; } = Array.Empty<string>();
    public ReviewStatus ReviewStatus { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Job Job { get; set; } = null!;
    public TextSegment Segment { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
