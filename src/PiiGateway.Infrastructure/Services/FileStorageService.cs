using Microsoft.Extensions.Options;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;

    public FileStorageService(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(Guid jobId, string extension, Stream content)
    {
        var filePath = GetFilePath(jobId, extension);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);
        return filePath;
    }

    public Task<Stream> OpenReadAsync(Guid jobId, string extension)
    {
        var filePath = GetFilePath(jobId, extension);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found for job {jobId}", filePath);

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(Guid jobId, string extension)
    {
        var filePath = GetFilePath(jobId, extension);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public string GetFilePath(Guid jobId, string extension)
    {
        var normalizedExt = extension.StartsWith('.') ? extension : $".{extension}";
        return Path.Combine(_options.BasePath, jobId.ToString(), $"original{normalizedExt}");
    }
}
