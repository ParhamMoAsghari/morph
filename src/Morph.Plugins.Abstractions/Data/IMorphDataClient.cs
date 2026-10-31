namespace Morph.Plugins.Abstractions;

/// <summary>
/// Access-enforced data port for plugins. Host refuses undeclared data.* permissions.
/// </summary>
public interface IMorphDataClient
{
    Task<IReadOnlyDictionary<string, object?>?> GetAsync(
        string entityName,
        string id,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        string entityName,
        string id,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken);
}
