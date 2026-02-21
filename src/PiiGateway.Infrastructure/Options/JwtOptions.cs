namespace PiiGateway.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PiiGateway";
    public string Audience { get; set; } = "PiiGateway";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshExpiryDays { get; set; } = 7;
}
