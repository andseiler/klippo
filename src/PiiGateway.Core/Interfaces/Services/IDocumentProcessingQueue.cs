namespace PiiGateway.Core.Interfaces.Services;

public interface IDocumentProcessingQueue
{
    Task EnqueueAsync(Guid jobId);
}
