using PiiGateway.Core.DTOs.Detection;

namespace PiiGateway.Core.Interfaces.Services;

public interface IPiiDetectionClient
{
    Task<DetectResponse> DetectAsync(DetectRequest request, CancellationToken ct = default);
}
