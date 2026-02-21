namespace PiiGateway.Core.DTOs.Detection;

public class LlmScanStartRequest
{
    public string? Instructions { get; set; }
    public string? Language { get; set; }
}
