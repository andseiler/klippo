using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PiiGateway.Tests.Integration.Fixtures;

namespace PiiGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class FullFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FullFlowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListJobs_ReturnsPagedResult()
    {
        var client = _factory.CreateClient().WithAuth();

        var response = await client.GetAsync("/api/v1/jobs?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.GetProperty("page").GetInt32().Should().Be(1);
        result.GetProperty("pageSize").GetInt32().Should().Be(10);
        result.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetJob_NonExistent_Returns404()
    {
        var client = _factory.CreateClient().WithAuth();

        var response = await client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsComponents()
    {
        var client = _factory.CreateClient().WithAuth();

        var response = await client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.GetProperty("components").GetProperty("database").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("components").GetProperty("redis").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("components").GetProperty("piiService").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
    }

}
