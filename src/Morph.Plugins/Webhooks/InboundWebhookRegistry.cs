using System.Collections.Concurrent;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Webhooks;

public sealed class InboundWebhookRegistry : IInboundWebhookRegistry
{
    private readonly ConcurrentDictionary<string, InboundWebhookRegistration> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, string pathSuffix, IInboundWebhookHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathSuffix);
        ArgumentNullException.ThrowIfNull(handler);

        var key = BuildKey(pluginId, pathSuffix);
        var registration = new InboundWebhookRegistration(pluginId, Normalize(pathSuffix), handler);
        if (!_handlers.TryAdd(key, registration))
        {
            throw new InvalidOperationException(
                $"Inbound webhook '{pluginId}/{pathSuffix}' is already registered.");
        }
    }

    public IReadOnlyCollection<InboundWebhookRegistration> GetAll() => _handlers.Values.ToArray();

    public bool TryResolve(string pluginId, string pathSuffix, out InboundWebhookRegistration registration) =>
        _handlers.TryGetValue(BuildKey(pluginId, pathSuffix), out registration!);

    private static string BuildKey(string pluginId, string pathSuffix) =>
        $"{pluginId}:{Normalize(pathSuffix)}";

    private static string Normalize(string pathSuffix) =>
        pathSuffix.Trim().Trim('/').ToLowerInvariant();
}
