using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Data;

/// <summary>
/// Default data client used when the plugin lacks data permissions or Access is not yet wired.
/// </summary>
public sealed class DeniedMorphDataClient : IMorphDataClient
{
    private readonly string _pluginId;
    private readonly bool _canRead;
    private readonly bool _canWrite;

    public DeniedMorphDataClient(string pluginId, bool canRead, bool canWrite)
    {
        _pluginId = pluginId;
        _canRead = canRead;
        _canWrite = canWrite;
    }

    public Task<IReadOnlyDictionary<string, object?>?> GetAsync(
        string entityName,
        string id,
        CancellationToken cancellationToken)
    {
        if (!_canRead)
        {
            throw new PluginPermissionException(_pluginId, PluginPermissions.DataRead, "data.get");
        }

        // Access + EntityEngine wiring lands with Process Engine / Runtime Data API.
        return Task.FromResult<IReadOnlyDictionary<string, object?>?>(null);
    }

    public Task UpsertAsync(
        string entityName,
        string id,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        if (!_canWrite)
        {
            throw new PluginPermissionException(_pluginId, PluginPermissions.DataWrite, "data.upsert");
        }

        throw new NotSupportedException(
            "Morph data writes from plugins require Access + EntityEngine runtime (not yet wired).");
    }
}
