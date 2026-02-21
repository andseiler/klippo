namespace PiiGateway.Infrastructure.Options;

public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public int CompletedJobRetentionDays { get; set; } = 90;
    public int AuditLogRetentionDays { get; set; } = 365;
    public string RunAtUtc { get; set; } = "03:00";
}
