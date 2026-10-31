using Morph.Plugins.Abstractions;

namespace Morph.ProcessEngine.Http;

/// <summary>
/// Fallback when Plugins DI is not loaded (unit tests). Prefer SSRF-safe factory in Host.
/// </summary>
public sealed class NullPluginHttpClientFactory : IPluginHttpClientFactory
{
    private readonly HttpClient _client = new();

    public HttpClient CreateClient(string? name = null) => _client;
}

/// <summary>
/// Empty plugin activity registry for hosts that have not loaded Morph.Plugins.
/// </summary>
public sealed class EmptyProcessActivityRegistry : IProcessActivityRegistry
{
    public void Register(string pluginId, string typeName, Type activityType)
    {
    }

    public bool TryGet(string typeName, out ProcessActivityDescriptor descriptor)
    {
        descriptor = null!;
        return false;
    }

    public IReadOnlyCollection<ProcessActivityDescriptor> GetAll() => [];
}
