using System.Collections.Concurrent;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Process;

public sealed class ProcessActivityRegistry : IProcessActivityRegistry
{
    private readonly ConcurrentDictionary<string, ProcessActivityDescriptor> _activities =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, string typeName, Type activityType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(activityType);

        if (!typeof(IProcessActivity).IsAssignableFrom(activityType))
        {
            throw new ArgumentException(
                $"Type '{activityType.FullName}' does not implement {nameof(IProcessActivity)}.",
                nameof(activityType));
        }

        var descriptor = new ProcessActivityDescriptor(pluginId, typeName, activityType);
        if (!_activities.TryAdd(typeName, descriptor))
        {
            throw new InvalidOperationException(
                $"Process activity '{typeName}' is already registered.");
        }
    }

    public bool TryGet(string typeName, out ProcessActivityDescriptor descriptor) =>
        _activities.TryGetValue(typeName, out descriptor!);

    public IReadOnlyCollection<ProcessActivityDescriptor> GetAll() =>
        _activities.Values.ToArray();
}
