namespace Morph.Crypto.Options;

/// <summary>
/// Master key material for project-scoped AEAD key derivation (NSec XChaCha20-Poly1305).
/// Prefer configuring via <c>Morph:Crypto:MasterKeyBase64</c> or <c>MORPH_Crypto__MasterKeyBase64</c>.
/// </summary>
public sealed class MorphCryptoOptions
{
    public const string SectionName = "Morph:Crypto";

    /// <summary>
    /// Base64-encoded 32-byte master key. When empty, a development-only stub key is derived
    /// (not suitable for production).
    /// </summary>
    public string MasterKeyBase64 { get; set; } = string.Empty;
}
