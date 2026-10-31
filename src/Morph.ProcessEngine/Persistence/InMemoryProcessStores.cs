using System.Collections.Concurrent;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ports;

namespace Morph.ProcessEngine.Persistence;

public sealed class InMemoryProcessDefinitionStore : IProcessDefinitionStore
{
    private readonly ConcurrentDictionary<Guid, ProcessDefinition> _definitions = new();
    private readonly ConcurrentDictionary<Guid, ProcessDefinitionVersion> _versions = new();
    private readonly object _gate = new();

    public Task<ProcessDefinition?> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        _definitions.TryGetValue(definitionId, out var d);
        return Task.FromResult(d);
    }

    public Task<ProcessDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var match = _definitions.Values.FirstOrDefault(d =>
            d.EnvironmentId == environmentId
            && string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<ProcessDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessDefinition> list = _definitions.Values
            .Where(d => d.EnvironmentId == environmentId)
            .OrderBy(d => d.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<ProcessDefinitionVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        _versions.TryGetValue(versionId, out var v);
        return Task.FromResult(v);
    }

    public Task<IReadOnlyList<ProcessDefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessDefinitionVersion> list = _versions.Values
            .Where(v => v.DefinitionId == definitionId)
            .OrderBy(v => v.VersionNumber)
            .ToList();
        return Task.FromResult(list);
    }

    public Task SaveDefinitionAsync(ProcessDefinition definition, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _definitions[definition.DefinitionId] = definition;
        }

        return Task.CompletedTask;
    }

    public Task SaveVersionAsync(ProcessDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_versions.TryGetValue(version.VersionId, out var existing)
                && existing.Status == ProcessVersionStatus.Published
                && existing.AuthoringJson != version.AuthoringJson
                && version.Status == ProcessVersionStatus.Published)
            {
                throw new InvalidOperationException("Published process versions are immutable.");
            }

            _versions[version.VersionId] = version;
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryProcessInstanceStore : IProcessInstanceStore
{
    private readonly ConcurrentDictionary<Guid, ProcessInstance> _instances = new();
    private readonly ConcurrentDictionary<Guid, List<ProcessActivityLogEntry>> _logs = new();
    private readonly object _gate = new();

    public Task<ProcessInstance?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        _instances.TryGetValue(instanceId, out var i);
        return Task.FromResult(i);
    }

    public Task<IReadOnlyList<ProcessInstance>> ListByDefinitionAsync(
        Guid definitionId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessInstance> list = _instances.Values
            .Where(i => i.DefinitionId == definitionId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(take)
            .ToList();
        return Task.FromResult(list);
    }

    public Task SaveAsync(ProcessInstance instance, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _instances[instance.InstanceId] = instance;
        }

        return Task.CompletedTask;
    }

    public Task AppendLogAsync(ProcessActivityLogEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var list = _logs.GetOrAdd(entry.InstanceId, _ => []);
            list.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcessActivityLogEntry>> ListLogsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_logs.TryGetValue(instanceId, out var list))
        {
            return Task.FromResult<IReadOnlyList<ProcessActivityLogEntry>>([]);
        }

        IReadOnlyList<ProcessActivityLogEntry> snapshot = list.OrderBy(l => l.StartedAt).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ProcessInstance>> ListWaitingDueAsync(
        DateTimeOffset asOf,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessInstance> list = _instances.Values
            .Where(i =>
                i.Status == ProcessInstanceStatus.Waiting
                && i.WaitingUntil is { } until
                && until <= asOf)
            .OrderBy(i => i.WaitingUntil)
            .Take(take)
            .ToList();
        return Task.FromResult(list);
    }
}
