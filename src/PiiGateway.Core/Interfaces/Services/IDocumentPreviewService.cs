using PiiGateway.Core.DTOs.Jobs;

namespace PiiGateway.Core.Interfaces.Services;

public interface IDocumentPreviewService
{
    Task<DocumentPreviewResponse> GetDocumentPreviewAsync(Guid jobId);
}
