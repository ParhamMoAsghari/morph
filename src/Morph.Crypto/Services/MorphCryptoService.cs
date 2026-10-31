using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Morph.Crypto.Options;
using Morph.Crypto.Ports;
using NSec.Cryptography;

namespace Morph.Crypto.Services;

/// <summary>
/// Envelope helpers using NSec <see cref="AeadAlgorithm.XChaCha20Poly1305"/> with
/// project-scoped keys derived via .NET HKDF-SHA256 from a configured master key.
/// </summary>
public sealed class MorphCryptoService : IMorphCrypto
{
    public const string FieldMarkerPrefix = "morph:enc:v1:";

    private static readonly AeadAlgorithm Algorithm = AeadAlgorithm.XChaCha20Poly1305;
    private static readonly byte[] DevelopmentStubSeed =
        "Morph.Crypto.Development.MasterKey.v1"u8.ToArray();

    private readonly byte[] _masterKey;

    public MorphCryptoService(IOptions<MorphCryptoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _masterKey = ResolveMasterKey(options.Value);
    }

    public byte[] Encrypt(Guid projectId, ReadOnlySpan<byte> plaintext)
    {
        using var key = CreateProjectKey(projectId);
        var nonce = new byte[Algorithm.NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var associatedData = projectId.ToByteArray();
        var cipher = Algorithm.Encrypt(key, nonce, associatedData, plaintext);

        var result = new byte[nonce.Length + cipher.Length];
        nonce.CopyTo(result.AsSpan());
        cipher.CopyTo(result.AsSpan(nonce.Length));
        return result;
    }

    public byte[] Decrypt(Guid projectId, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < Algorithm.NonceSize + Algorithm.TagSize)
        {
            throw new CryptographicException("Ciphertext is too short.");
        }

        var nonce = ciphertext[..Algorithm.NonceSize];
        var cipher = ciphertext[Algorithm.NonceSize..];
        using var key = CreateProjectKey(projectId);
        var associatedData = projectId.ToByteArray();

        return Algorithm.Decrypt(key, nonce, associatedData, cipher)
            ?? throw new CryptographicException("Decryption failed (authentication tag mismatch).");
    }

    public string Encrypt(Guid projectId, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encrypt(projectId, Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(bytes);
    }

    public string Decrypt(Guid projectId, string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        var bytes = Decrypt(projectId, Convert.FromBase64String(ciphertext));
        return Encoding.UTF8.GetString(bytes);
    }

    public string EncryptField(Guid projectId, string? plaintext)
    {
        var value = plaintext ?? string.Empty;
        var encrypted = Encrypt(projectId, Encoding.UTF8.GetBytes(value));
        return FieldMarkerPrefix + Base64Url.EncodeToString(encrypted);
    }

    public string? DecryptField(Guid projectId, string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        if (!stored.StartsWith(FieldMarkerPrefix, StringComparison.Ordinal))
        {
            return stored;
        }

        var payload = stored[FieldMarkerPrefix.Length..];
        if (!Base64Url.TryDecodeFromChars(payload, out var encrypted))
        {
            throw new CryptographicException("Encrypted field marker is not valid base64url.");
        }

        var plain = Decrypt(projectId, encrypted);
        return Encoding.UTF8.GetString(plain);
    }

    private Key CreateProjectKey(Guid projectId)
    {
        Span<byte> derived = stackalloc byte[32];
        var info = Encoding.UTF8.GetBytes($"morph-crypto:project:{projectId:N}");
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _masterKey, derived, salt: default, info: info);

        return Key.Import(
            Algorithm,
            derived,
            KeyBlobFormat.RawSymmetricKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.None });
    }

    private static byte[] ResolveMasterKey(MorphCryptoOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.MasterKeyBase64))
        {
            var key = Convert.FromBase64String(options.MasterKeyBase64.Trim());
            if (key.Length != 32)
            {
                throw new InvalidOperationException(
                    "Morph crypto master key must be exactly 32 bytes (base64-encoded).");
            }

            return key;
        }

        // Development stub only — production must set MasterKeyBase64.
        return SHA256.HashData(DevelopmentStubSeed);
    }

    private static class Base64Url
    {
        public static string EncodeToString(ReadOnlySpan<byte> data)
        {
            var base64 = Convert.ToBase64String(data);
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static bool TryDecodeFromChars(ReadOnlySpan<char> text, out byte[] bytes)
        {
            var padded = text.ToString().Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            try
            {
                bytes = Convert.FromBase64String(padded);
                return true;
            }
            catch (FormatException)
            {
                bytes = [];
                return false;
            }
        }
    }
}
