namespace Morph.Crypto.Ports;

/// <summary>
/// Project-scoped envelope encryption helpers (NSec XChaCha20-Poly1305).
/// </summary>
public interface IMorphCrypto
{
    byte[] Encrypt(Guid projectId, ReadOnlySpan<byte> plaintext);

    byte[] Decrypt(Guid projectId, ReadOnlySpan<byte> ciphertext);

    string Encrypt(Guid projectId, string plaintext);

    string Decrypt(Guid projectId, string ciphertext);

    /// <summary>
    /// Encrypts a field value and returns a ciphertext marker suitable for storage
    /// (e.g. <c>morph:enc:v1:...</c>).
    /// </summary>
    string EncryptField(Guid projectId, string? plaintext);

    /// <summary>
    /// Decrypts a ciphertext marker produced by <see cref="EncryptField"/>.
    /// Plain (non-marker) values are returned unchanged.
    /// </summary>
    string? DecryptField(Guid projectId, string? stored);
}
