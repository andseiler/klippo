using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Auth;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;
using PiiGateway.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace PiiGateway.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IJwtService _jwtService;
    private readonly JwtOptions _jwtOptions;
    private readonly GuestDemoOptions _guestOptions;
    private readonly ILogger<AuthController> _logger;
    private readonly PlaygroundUsageTracker _usageTracker;

    public AuthController(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IJwtService jwtService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<GuestDemoOptions> guestOptions,
        ILogger<AuthController> logger,
        PlaygroundUsageTracker usageTracker)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _jwtService = jwtService;
        _jwtOptions = jwtOptions.Value;
        _guestOptions = guestOptions.Value;
        _logger = logger;
        _usageTracker = usageTracker;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtOptions.RefreshExpiryDays);
        await _userRepository.UpdateAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role.ToString(),
            OrganizationId = user.OrganizationId
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Find user by refresh token
        // This is a simplified approach; in production, use a dedicated token store
        var users = await Task.Run(() => (User?)null);

        // For now, we need to search by token - this will be optimized later
        return Unauthorized(new { message = "Invalid refresh token" });
    }

    [HttpPost("guest")]
    [EnableRateLimiting("guest")]
    public async Task<IActionResult> Guest()
    {
        if (!_guestOptions.Enabled)
            return NotFound(new { message = "Guest access is not enabled." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_usageTracker.TryUse(ip))
            return StatusCode(429, new { message = "Daily playground limit reached. Please register for unlimited access." });

        var user = await _userRepository.GetByIdAsync(_guestOptions.UserId);
        if (user == null)
            return StatusCode(503, new { message = "Guest demo user not configured." });

        var accessToken = _jwtService.GenerateAccessToken(user);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = string.Empty,
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role.ToString(),
            OrganizationId = user.OrganizationId
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Invalidate refresh token based on the authenticated user's claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _userRepository.UpdateAsync(user);
            }
        }

        return Ok(new { message = "Logged out successfully" });
    }
}
