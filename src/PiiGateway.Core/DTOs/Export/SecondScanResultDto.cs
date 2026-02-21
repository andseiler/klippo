namespace PiiGateway.Core.DTOs.Export;

public class SecondScanResultDto
{
    public bool Passed { get; set; }
    public List<SecondScanDetection> Detections { get; set; } = new();
}

public class SecondScanDetection
{
    public string EntityType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
}
