using PiiGateway.Core.DTOs.Export;

namespace PiiGateway.Core.Interfaces.Services;

public interface IPseudonymizationService
{
    Task<string> PseudonymizeJobAsync(Guid jobId, Guid userId);
    Task GeneratePreviewTokensAsync(Guid jobId);
    string GenerateReplacement(string entityType, string originalText, string locale);
    Task<PseudonymizedOutputResponse> GetPseudonymizedOutputAsync(Guid jobId);
}
