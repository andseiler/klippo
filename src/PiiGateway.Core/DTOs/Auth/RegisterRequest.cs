using System.ComponentModel.DataAnnotations;

namespace PiiGateway.Core.DTOs.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string OrganizationName { get; set; } = string.Empty;
}
