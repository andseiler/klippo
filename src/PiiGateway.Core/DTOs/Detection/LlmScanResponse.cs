namespace PiiGateway.Core.DTOs.Detection;

public class LlmScanResponse
{
    public string Status { get; set; } = "pending";
    public int ProcessedSegments { get; set; }
    public int TotalSegments { get; set; }
    public List<LlmScanDetection> Detections { get; set; } = new();
    public string? Error { get; set; }
}

public class LlmScanDetection
{
    public Guid SegmentId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public double Confidence { get; set; }
    public string? OriginalText { get; set; }
    public Guid? EntityId { get; set; }
}
