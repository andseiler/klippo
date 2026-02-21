namespace PiiGateway.Core.DTOs.Review;

public class ReviewDataResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<SegmentDto> Segments { get; set; } = new();
    public List<EntityDto> Entities { get; set; } = new();
    public ReviewSummary Summary { get; set; } = new();
}

public class SegmentDto
{
    public Guid Id { get; set; }
    public int SegmentIndex { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceLocation { get; set; }
}

public class EntityDto
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public double Confidence { get; set; }
    public string[] DetectionSources { get; set; } = Array.Empty<string>();
    public string ConfidenceTier { get; set; } = string.Empty;
    public string? ReplacementPreview { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
}

public class ReviewSummary
{
    public int TotalEntities { get; set; }
    public int HighConfidence { get; set; }
    public int MediumConfidence { get; set; }
    public int LowConfidence { get; set; }
    public int Confirmed { get; set; }
    public int ManuallyAdded { get; set; }
    public int Pending { get; set; }
}
