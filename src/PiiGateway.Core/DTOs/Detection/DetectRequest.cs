namespace PiiGateway.Core.DTOs.Detection;

public class DetectRequest
{
    public Guid JobId { get; set; }
    public List<DetectionSegment> Segments { get; set; } = new();
    public List<string> Layers { get; set; } = new();
    public string? LanguageHint { get; set; }
    public List<ExistingDetection> ExistingDetections { get; set; } = new();
    public string? CustomInstructions { get; set; }
}

public class DetectionSegment
{
    public Guid SegmentId { get; set; }
    public int SegmentIndex { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
}

public class ExistingDetection
{
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
}
