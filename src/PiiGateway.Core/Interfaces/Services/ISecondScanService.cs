using PiiGateway.Core.DTOs.Export;

namespace PiiGateway.Core.Interfaces.Services;

public interface ISecondScanService
{
    Task<SecondScanResultDto> RunSecondScanAsync(Guid jobId, Guid userId, string? ipAddress);
    Task<SecondScanResultDto> ScanPseudonymizedTextOnlyAsync(Guid jobId);
}
