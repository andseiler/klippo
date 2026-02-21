namespace PiiGateway.Infrastructure.Options;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string BasePath { get; set; } = "/app/uploads";
    public int MaxFileSizeMb { get; set; } = 50;
}
