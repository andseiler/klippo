using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendRegistrationNotificationAsync(string email, string name, string organizationName)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Email is disabled. Skipping registration notification for {Email}.", email);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(_options.NotificationRecipient));
        message.Subject = $"New registration: {name} ({organizationName})";

        message.Body = new TextPart("plain")
        {
            Text = $"""
                New user registered on Klippo:

                Name: {name}
                Email: {email}
                Organization: {organizationName}
                Time: {DateTime.UtcNow:u}
                """
        };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl);

            if (!string.IsNullOrEmpty(_options.SmtpUser))
            {
                await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Registration notification sent for {Email}.", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration notification for {Email}.", email);
        }
    }
}
