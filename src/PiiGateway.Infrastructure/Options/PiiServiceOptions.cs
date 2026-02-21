namespace PiiGateway.Infrastructure.Options;

public class PiiServiceOptions
{
    public const string SectionName = "PiiService";

    public string BaseUrl { get; set; } = "http://pii-service:8001";
    public int TimeoutSeconds { get; set; } = 120;
    public List<string> Layers { get; set; } = new() { "regex", "ner" };
}
