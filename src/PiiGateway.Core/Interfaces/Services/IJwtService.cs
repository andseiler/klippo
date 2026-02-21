using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Core.Interfaces.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token);
}
