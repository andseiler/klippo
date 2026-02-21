namespace PiiGateway.Core.DTOs.Review;

public class AddEntityRequest
{
    public Guid SegmentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string? ReplacementText { get; set; }
}
