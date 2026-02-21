using PiiGateway.Core.DTOs.Export;

namespace PiiGateway.Core.DTOs.Review;

public class CompleteReviewResultDto
{
    public bool ScanPassed { get; set; }
    public List<SecondScanDetection> Detections { get; set; } = new();
}
