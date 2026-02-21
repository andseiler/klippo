namespace PiiGateway.Infrastructure.Options;

public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) AES key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
