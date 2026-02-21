namespace PiiGateway.Core.DTOs.Export;

public class PseudonymizedOutputResponse
{
    public string PseudonymizedText { get; set; } = string.Empty;
    public List<ReplacementEntry> Replacements { get; set; } = new();
}

public class ReplacementEntry
{
    public string Original { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
}
