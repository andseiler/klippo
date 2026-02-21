namespace PiiGateway.Core.DTOs.Detection;

public class DetectResponse
{
    public List<DetectionResult> Detections { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public List<string> LayersUsed { get; set; } = new();
    public List<LayerStat> LayerStats { get; set; } = new();
}

public class LayerStat
{
    public string Layer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DetectionCount { get; set; }
    public string? SkipReason { get; set; }
}

public class DetectionResult
{
    public Guid SegmentId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public double Confidence { get; set; }
    public string DetectionSource { get; set; } = string.Empty;
    public string? OriginalText { get; set; }
}
