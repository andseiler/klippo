using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace PiiGateway.Tests.Integration;

public class JobsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JobsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5433;Database=piigateway_test;Username=piigateway;Password=changeme_dev_only",
                    ["ConnectionStrings:Redis"] = "localhost:6379",
                    ["FileStorage:BasePath"] = Path.Combine(Path.GetTempPath(), "piigateway_test_uploads"),
                    ["FileStorage:MaxFileSizeMb"] = "50",
                });
            });
        });
    }

    [Fact]
    public async Task CreateJob_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "test.pdf");

        var response = await client.PostAsync("/api/v1/jobs", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateJob_MissingFile_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var content = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/v1/jobs", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateJob_TextFile_IsAccepted()
    {
        var client = CreateAuthenticatedClient();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        var response = await client.PostAsync("/api/v1/jobs", content);

        // Should be accepted (202) — plaintext files are now supported
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetJob_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();

        var jwtSecret = "dev_secret_replace_in_production_minimum_64_characters_long!!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "User"),
            new Claim("org_id", Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "PiiGateway",
            audience: "PiiGateway",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        return client;
    }

    private static byte[] CreateMinimalPdf()
    {
        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("Test content", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        return builder.Build();
    }
}
