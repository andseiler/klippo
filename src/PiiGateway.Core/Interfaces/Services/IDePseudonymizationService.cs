using PiiGateway.Core.DTOs.Export;

namespace PiiGateway.Core.Interfaces.Services;

public interface IDePseudonymizationService
{
    Task<DePseudonymizedResponse> DePseudonymizeAsync(Guid jobId, string llmResponseText, Guid userId, string? ipAddress);
}
