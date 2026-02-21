namespace PiiGateway.Core.Interfaces.Services;

public interface IDocumentProcessor
{
    Task ProcessAsync(Guid jobId);
}
