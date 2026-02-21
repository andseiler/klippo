namespace PiiGateway.Core.DTOs.Review;

public class UpdateEntityRequest
{
    public string? ReviewStatus { get; set; }
    public string? EntityType { get; set; }
    public int? StartOffset { get; set; }
    public int? EndOffset { get; set; }
    public string? ReplacementText { get; set; }
}
