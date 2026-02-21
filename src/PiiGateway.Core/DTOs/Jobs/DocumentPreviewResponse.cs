using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.DTOs.Review;

namespace PiiGateway.Core.DTOs.Jobs;

public class DocumentPreviewResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasDocumentData { get; set; }
    public List<SegmentDto> Segments { get; set; } = new();
    public List<EntityDto> Entities { get; set; } = new();
    public string? PseudonymizedText { get; set; }
    public List<ReplacementEntry>? Replacements { get; set; }
}
