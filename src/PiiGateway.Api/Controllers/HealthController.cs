using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly PiiGatewayDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public HealthController(
        PiiGatewayDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IDistributedCache cache)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var dbHealthy = false;
        var piiServiceHealthy = false;
        var redisHealthy = false;
        var llmAvailable = false;

        try
        {
            dbHealthy = await _dbContext.Database.CanConnectAsync();
        }
        catch
        {
            // DB not reachable
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var piiBaseUrl = _configuration["PiiService:BaseUrl"] ?? "http://pii-service:8001";
            var response = await client.GetAsync($"{piiBaseUrl}/health");
            piiServiceHealthy = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("layers_available", out var layers))
                {
                    llmAvailable = layers.EnumerateArray()
                        .Any(l => l.GetString() == "llm");
                }
            }
        }
        catch
        {
            // PII service not reachable
        }

        try
        {
            var testKey = "__health_check__";
            await _cache.SetStringAsync(testKey, "ok", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            });
            var value = await _cache.GetStringAsync(testKey);
            redisHealthy = value == "ok";
        }
        catch
        {
            // Redis not reachable
        }

        var allHealthy = dbHealthy && redisHealthy;
        var status = allHealthy ? "healthy" : "degraded";

        // Unauthenticated requests get a simple status response
        var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

        if (!isAdmin)
        {
            return Ok(new { status });
        }

        // Authenticated admin requests get detailed component info
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        return Ok(new
        {
            status,
            version,
            llmAvailable,
            components = new
            {
                database = dbHealthy ? "connected" : "disconnected",
                redis = redisHealthy ? "connected" : "disconnected",
                piiService = piiServiceHealthy ? "connected" : "disconnected"
            },
            timestamp = DateTime.UtcNow
        });
    }
}
