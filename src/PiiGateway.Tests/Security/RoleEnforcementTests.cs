using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using PiiGateway.Tests.Integration.Fixtures;

namespace PiiGateway.Tests.Security;

[Trait("Category", "Security")]
public class RoleEnforcementTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RoleEnforcementTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteJob_WithUserRole_Returns403()
    {
        var client = _factory.CreateClient().WithAuth("User");

        var response = await client.DeleteAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteJob_WithReviewerRole_Returns403()
    {
        var client = _factory.CreateClient().WithAuth("Reviewer");

        var response = await client.DeleteAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteJob_WithAdminRole_DoesNotReturn403()
    {
        var client = _factory.CreateClient().WithAuth("Admin");

        // Will return 404 (job doesn't exist) but NOT 403
        var response = await client.DeleteAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("PATCH", "/api/v1/jobs/{0}/entities/{1}")]
    [InlineData("POST", "/api/v1/jobs/{0}/entities")]
    [InlineData("POST", "/api/v1/jobs/{0}/confirm")]
    [InlineData("POST", "/api/v1/jobs/{0}/complete-review")]
    [InlineData("POST", "/api/v1/jobs/{0}/reopen-review")]
    [InlineData("POST", "/api/v1/jobs/{0}/second-scan")]
    [InlineData("POST", "/api/v1/jobs/{0}/deanonymize")]
    public async Task ReviewerEndpoints_WithUserRole_Returns403(string method, string urlTemplate)
    {
        var client = _factory.CreateClient().WithAuth("User");
        var url = string.Format(urlTemplate, Guid.NewGuid(), Guid.NewGuid());

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method != "GET")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"{method} {urlTemplate} should require Reviewer role");
    }

    [Theory]
    [InlineData("POST", "/api/v1/jobs/{0}/confirm")]
    [InlineData("POST", "/api/v1/jobs/{0}/complete-review")]
    [InlineData("POST", "/api/v1/jobs/{0}/second-scan")]
    public async Task ReviewerEndpoints_WithReviewerRole_DoesNotReturn403(string method, string urlTemplate)
    {
        var client = _factory.CreateClient().WithAuth("Reviewer");
        var url = string.Format(urlTemplate, Guid.NewGuid(), Guid.NewGuid());

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        // Should not be 403 — may be 404 (job not found) or 400 but not Forbidden
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            $"{method} {urlTemplate} should allow Reviewer role");
    }

    [Fact]
    public async Task ListJobs_WithUserRole_IsAllowed()
    {
        var client = _factory.CreateClient().WithAuth("User");

        var response = await client.GetAsync("/api/v1/jobs");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateJob_WithUserRole_IsAllowed()
    {
        var client = _factory.CreateClient().WithAuth("User");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        var response = await client.PostAsync("/api/v1/jobs", content);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Should().ContainKey("Referrer-Policy");
    }
}
