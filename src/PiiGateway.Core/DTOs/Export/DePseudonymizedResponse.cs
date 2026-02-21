namespace PiiGateway.Core.DTOs.Export;

public class DePseudonymizedResponse
{
    public string DepseudonymizedText { get; set; } = string.Empty;
    public List<ReplacementMade> ReplacementsMade { get; set; } = new();
    public List<string> UnmappedWarnings { get; set; } = new();
}

public class ReplacementMade
{
    public string Pseudonym { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public int Count { get; set; }
}
