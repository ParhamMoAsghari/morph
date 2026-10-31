namespace Morph.Plugins.Abstractions;

/// <summary>
/// Process Engine activity contract. Process Engine (Phase 8+) resolves these by type name.
/// </summary>
public interface IProcessActivity
{
    string TypeName { get; }

    Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken);
}

public interface IProcessActivityContext
{
    IReadOnlyDictionary<string, object?> Variables { get; }

    void SetVariable(string name, object? value);

    IPluginHttpClientFactory Http { get; }
}

/// <summary>
/// Stub registry consumed later by Process Engine. Plugins register activities here at load time.
/// </summary>
public interface IProcessActivityRegistry
{
    void Register(string pluginId, string typeName, Type activityType);

    bool TryGet(string typeName, out ProcessActivityDescriptor descriptor);

    IReadOnlyCollection<ProcessActivityDescriptor> GetAll();
}

public sealed record ProcessActivityDescriptor(
    string PluginId,
    string TypeName,
    Type ActivityType);
