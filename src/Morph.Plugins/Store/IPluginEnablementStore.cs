using System.Collections.Concurrent;
using System.Text.Json;

namespace Morph.Plugins.Store;

public interface IPluginEnablementStore
{
    bool IsEnabled(string pluginId);

    void SetEnabled(string pluginId, bool enabled);

    IReadOnlyDictionary<string, JsonElement>? GetConfig(string pluginId);

    void SetConfig(string pluginId, IReadOnlyDictionary<string, JsonElement> config);

    IReadOnlyCollection<PluginEnablementState> List();
}

public sealed record PluginEnablementState(
    string PluginId,
    bool Enabled,
    IReadOnlyDictionary<string, JsonElement>? Config);

/// <summary>
/// In-memory enable/config store. Persist via EF in a later phase if needed.
/// </summary>
public sealed class InMemoryPluginEnablementStore : IPluginEnablementStore
{
    private readonly ConcurrentDictionary<string, PluginEnablementState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly bool _defaultEnabled;

    public InMemoryPluginEnablementStore(bool defaultEnabled = true)
    {
        _defaultEnabled = defaultEnabled;
    }

    public bool IsEnabled(string pluginId)
    {
        if (_states.TryGetValue(pluginId, out var state))
        {
            return state.Enabled;
        }

        return _defaultEnabled;
    }

    public void SetEnabled(string pluginId, bool enabled)
    {
        _states.AddOrUpdate(
            pluginId,
            _ => new PluginEnablementState(pluginId, enabled, null),
            (_, existing) => existing with { Enabled = enabled });
    }

    public IReadOnlyDictionary<string, JsonElement>? GetConfig(string pluginId) =>
        _states.TryGetValue(pluginId, out var state) ? state.Config : null;

    public void SetConfig(string pluginId, IReadOnlyDictionary<string, JsonElement> config)
    {
        _states.AddOrUpdate(
            pluginId,
            _ => new PluginEnablementState(pluginId, _defaultEnabled, config),
            (_, existing) => existing with { Config = config });
    }

    public IReadOnlyCollection<PluginEnablementState> List() => _states.Values.ToArray();
}
