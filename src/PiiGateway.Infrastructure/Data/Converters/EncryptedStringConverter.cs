using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Data.Converters;

public class EncryptedStringConverter : ValueConverter<string?, string?>
{
    private static IEncryptionService? _encryptionService;

    public static void SetEncryptionService(IEncryptionService service)
    {
        _encryptionService = service;
    }

    public static readonly EncryptedStringConverter Instance = new();

    public EncryptedStringConverter()
        : base(
            v => Encrypt(v),
            v => Decrypt(v))
    {
    }

    private static string? Encrypt(string? value)
    {
        if (value == null) return null;
        if (_encryptionService == null)
            throw new InvalidOperationException("EncryptionService has not been configured for EncryptedStringConverter.");
        return _encryptionService.Encrypt(value);
    }

    private static string? Decrypt(string? value)
    {
        if (value == null) return null;
        if (_encryptionService == null)
            throw new InvalidOperationException("EncryptionService has not been configured for EncryptedStringConverter.");
        return _encryptionService.Decrypt(value);
    }
}
