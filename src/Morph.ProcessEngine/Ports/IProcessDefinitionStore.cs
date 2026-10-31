using Morph.ProcessEngine.Domain;

namespace Morph.ProcessEngine.Ports;

public interface IProcessDefinitionStore
{
    Task<ProcessDefinition?> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessDefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task SaveDefinitionAsync(ProcessDefinition definition, CancellationToken cancellationToken = default);

    Task SaveVersionAsync(ProcessDefinitionVersion version, CancellationToken cancellationToken = default);
}

public interface IProcessInstanceStore
{
    Task<ProcessInstance?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessInstance>> ListByDefinitionAsync(
        Guid definitionId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task SaveAsync(ProcessInstance instance, CancellationToken cancellationToken = default);

    Task AppendLogAsync(ProcessActivityLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessActivityLogEntry>> ListLogsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessInstance>> ListWaitingDueAsync(
        DateTimeOffset asOf,
        int take = 50,
        CancellationToken cancellationToken = default);
}
