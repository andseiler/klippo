namespace PiiGateway.Core.Interfaces.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Guid jobId, string extension, Stream content);
    Task<Stream> OpenReadAsync(Guid jobId, string extension);
    Task DeleteAsync(Guid jobId, string extension);
    string GetFilePath(Guid jobId, string extension);
}
