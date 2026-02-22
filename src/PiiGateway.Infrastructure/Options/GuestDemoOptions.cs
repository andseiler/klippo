namespace PiiGateway.Infrastructure.Options;

public class GuestDemoOptions
{
    public const string SectionName = "GuestDemo";

    public bool Enabled { get; set; }
    public Guid UserId { get; set; }
    public int TokenExpiryMinutes { get; set; } = 60;
    public int CleanupIntervalMinutes { get; set; } = 15;
}
