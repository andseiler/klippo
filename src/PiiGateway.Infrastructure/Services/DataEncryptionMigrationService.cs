using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Services;

/// <summary>
/// One-shot service to encrypt existing plaintext OriginalTextEnc records.
/// Detects unencrypted values (not valid Base64 or too short for nonce+tag) and encrypts them in-place.
/// </summary>
public class DataEncryptionMigrationService
{
    private const int MinEncryptedLength = 40; // Base64 of at least nonce(12) + tag(16) = 28 bytes → ~40 chars base64

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<DataEncryptionMigrationService> _logger;

    public DataEncryptionMigrationService(
        IServiceScopeFactory scopeFactory,
        IEncryptionService encryptionService,
        ILogger<DataEncryptionMigrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        // Use a separate DbContext without the converter to read raw values
        var optionsBuilder = new DbContextOptionsBuilder<PiiGatewayDbContext>();
        var existingContext = scope.ServiceProvider.GetRequiredService<PiiGatewayDbContext>();
        var connectionString = existingContext.Database.GetConnectionString();
        optionsBuilder.UseNpgsql(connectionString);

        await using var rawContext = new PiiGatewayDbContext(optionsBuilder.Options);

        var entities = await rawContext.PiiEntities
            .Where(e => e.OriginalTextEnc != null)
            .ToListAsync(cancellationToken);

        var migrated = 0;
        foreach (var entity in entities)
        {
            if (entity.OriginalTextEnc == null) continue;

            if (IsLikelyEncrypted(entity.OriginalTextEnc))
                continue;

            // This is plaintext — encrypt it
            var encrypted = _encryptionService.Encrypt(entity.OriginalTextEnc);
            entity.OriginalTextEnc = encrypted;
            migrated++;
        }

        if (migrated > 0)
        {
            await rawContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Encrypted {Count} plaintext PII records", migrated);
        }
        else
        {
            _logger.LogInformation("No plaintext PII records found — migration not needed");
        }
    }

    private static bool IsLikelyEncrypted(string value)
    {
        if (value.Length < MinEncryptedLength) return false;

        try
        {
            var bytes = Convert.FromBase64String(value);
            // nonce(12) + at least 1 byte ciphertext + tag(16) = 29 minimum
            return bytes.Length >= 29;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
