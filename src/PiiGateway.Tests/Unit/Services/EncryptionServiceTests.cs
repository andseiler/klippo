using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PiiGateway.Infrastructure.Options;
using PiiGateway.Infrastructure.Services;

namespace PiiGateway.Tests.Unit.Services;

public class EncryptionServiceTests
{
    private static AesGcmEncryptionService CreateService(byte[]? key = null)
    {
        key ??= GenerateKey();
        var options = Options.Create(new EncryptionOptions { Key = Convert.ToBase64String(key) });
        return new AesGcmEncryptionService(options);
    }

    private static byte[] GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        var service = CreateService();
        var plaintext = "Max Mustermann, Steuer-ID: 12345678901";

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertexts_DueToRandomNonce()
    {
        var service = CreateService();
        var plaintext = "Same text";

        var encrypted1 = service.Encrypt(plaintext);
        var encrypted2 = service.Encrypt(plaintext);

        encrypted1.Should().NotBe(encrypted2, "each encryption should use a unique random nonce");
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var key1 = GenerateKey();
        var key2 = GenerateKey();
        var service1 = CreateService(key1);
        var service2 = CreateService(key2);

        var encrypted = service1.Encrypt("secret data");

        var act = () => service2.Decrypt(encrypted);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void EncryptDecrypt_EmptyString_Works()
    {
        var service = CreateService();

        var encrypted = service.Encrypt("");
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be("");
    }

    [Fact]
    public void EncryptDecrypt_UnicodeText_Works()
    {
        var service = CreateService();
        var plaintext = "Müller-Thürgau Straße 42, Österreich";

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void EncryptDecrypt_LongText_Works()
    {
        var service = CreateService();
        var plaintext = new string('A', 10_000);

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Constructor_MissingKey_Throws()
    {
        var options = Options.Create(new EncryptionOptions { Key = "" });

        var act = () => new AesGcmEncryptionService(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public void Constructor_WrongKeyLength_Throws()
    {
        var shortKey = new byte[16]; // 128-bit instead of 256-bit
        var options = Options.Create(new EncryptionOptions { Key = Convert.ToBase64String(shortKey) });

        var act = () => new AesGcmEncryptionService(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*256 bits*");
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var service = CreateService();
        var encrypted = service.Encrypt("test data");

        // Tamper with the ciphertext
        var bytes = Convert.FromBase64String(encrypted);
        bytes[15] ^= 0xFF; // Flip a byte in the ciphertext area
        var tampered = Convert.ToBase64String(bytes);

        var act = () => service.Decrypt(tampered);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Decrypt_TooShort_Throws()
    {
        var service = CreateService();
        var tooShort = Convert.ToBase64String(new byte[10]);

        var act = () => service.Decrypt(tooShort);

        act.Should().Throw<CryptographicException>();
    }
}
