using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class AesGcmEncryptionService : IEncryptionService
{
    private const int NonceSize = 12; // 96 bits
    private const int TagSize = 16;   // 128 bits
    private readonly byte[] _key;

    public AesGcmEncryptionService(IOptions<EncryptionOptions> options)
    {
        var keyBase64 = options.Value.Key;
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException("Encryption key is not configured. Set the Encryption:Key configuration value.");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be exactly 256 bits (32 bytes) when decoded from Base64.");
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Output format: nonce || ciphertext || tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NonceSize);
        tag.CopyTo(result, NonceSize + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedBase64)
    {
        var combined = Convert.FromBase64String(encryptedBase64);

        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext: too short.");

        var nonce = combined.AsSpan(0, NonceSize);
        var ciphertextLength = combined.Length - NonceSize - TagSize;
        var ciphertext = combined.AsSpan(NonceSize, ciphertextLength);
        var tag = combined.AsSpan(NonceSize + ciphertextLength, TagSize);

        var plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
