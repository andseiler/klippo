using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    public Organization Organization { get; set; } = null!;
}
