namespace PiiGateway.Core.Interfaces.Services;

public interface IEmailService
{
    Task SendRegistrationNotificationAsync(string email, string name, string organizationName);
}
