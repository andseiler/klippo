namespace PiiGateway.Infrastructure.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Klippo";
    public string NotificationRecipient { get; set; } = string.Empty;
}
