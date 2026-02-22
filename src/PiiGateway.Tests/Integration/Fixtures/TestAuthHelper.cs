using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PiiGateway.Tests.Integration.Fixtures;

public static class TestAuthHelper
{
    public const string TestJwtSecret = "dev_secret_replace_in_production_minimum_64_characters_long!!";
    public const string TestIssuer = "PiiGateway";
    public const string TestAudience = "PiiGateway";

    public static string CreateToken(Guid? userId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new Claim(ClaimTypes.Email, "test@example.com"),
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient WithAuth(this HttpClient client, Guid? userId = null)
    {
        var token = CreateToken(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
