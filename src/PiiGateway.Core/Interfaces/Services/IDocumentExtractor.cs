using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Core.Interfaces.Services;

public interface IDocumentExtractor
{
    bool CanHandle(string fileType);
    Task<IReadOnlyList<TextSegment>> ExtractAsync(Stream stream, Guid jobId);
}
