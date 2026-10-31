using System.Collections.Concurrent;

namespace Morph.Plugins.Webhooks;

public interface IPluginHttpRouteRegistry
{
    void Register(string pluginId, string relativePath, Func<HttpContext, CancellationToken, Task> handler);

    bool TryResolve(string pluginId, string relativePath, out PluginHttpRouteRegistration registration);

    IReadOnlyCollection<PluginHttpRouteRegistration> GetAll();
}

public sealed record PluginHttpRouteRegistration(
    string PluginId,
    string RelativePath,
    Func<HttpContext, CancellationToken, Task> Handler);

public sealed class PluginHttpRouteRegistry : IPluginHttpRouteRegistry
{
    private readonly ConcurrentDictionary<string, PluginHttpRouteRegistration> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, string relativePath, Func<HttpContext, CancellationToken, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(handler);

        var normalized = relativePath.Trim().Trim('/');
        var key = $"{pluginId}:{normalized}";
        var registration = new PluginHttpRouteRegistration(pluginId, normalized, handler);
        if (!_routes.TryAdd(key, registration))
        {
            throw new InvalidOperationException($"HTTP route '{pluginId}/{normalized}' is already registered.");
        }
    }

    public bool TryResolve(string pluginId, string relativePath, out PluginHttpRouteRegistration registration) =>
        _routes.TryGetValue($"{pluginId}:{relativePath.Trim().Trim('/')}", out registration!);

    public IReadOnlyCollection<PluginHttpRouteRegistration> GetAll() => _routes.Values.ToArray();
}
