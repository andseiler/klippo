using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class PiiDetectionClient : IPiiDetectionClient
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;

    public PiiDetectionClient(HttpClient httpClient, IOptions<PiiServiceOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
    }

    public async Task<DetectResponse> DetectAsync(DetectRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/detect", request, CamelCaseOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"PII service returned {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<DetectResponse>(CamelCaseOptions, ct)
            ?? new DetectResponse();
    }
}
