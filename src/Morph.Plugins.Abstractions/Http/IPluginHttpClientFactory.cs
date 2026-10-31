namespace Morph.Plugins.Abstractions;

/// <summary>
/// Host-provided HTTP client factory with SSRF policy (blocks private/link-local targets).
/// Plugins must not create raw <see cref="HttpClient"/> for outbound calls.
/// </summary>
public interface IPluginHttpClientFactory
{
    HttpClient CreateClient(string? name = null);
}
