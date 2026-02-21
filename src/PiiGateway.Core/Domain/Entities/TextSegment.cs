using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain.Entities;

public class TextSegment
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int SegmentIndex { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public string? SourceLocation { get; set; }
    public DateTime CreatedAt { get; set; }

    public Job Job { get; set; } = null!;
}
