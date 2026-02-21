namespace PiiGateway.Core.DTOs.Review;

public class UpdateSegmentRequest
{
    public string TextContent { get; set; } = string.Empty;
    public List<EntityOffsetUpdate> EntityOffsets { get; set; } = new();
}

public class EntityOffsetUpdate
{
    public Guid EntityId { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string Text { get; set; } = string.Empty;
}
