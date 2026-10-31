using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Ports;

public interface IDefinitionStore
{
    Task<EntityDefinition?> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    Task<EntityDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntityDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default);

    Task<DefinitionVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task SaveDefinitionAsync(EntityDefinition definition, CancellationToken cancellationToken = default);

    Task SaveVersionAsync(DefinitionVersion version, CancellationToken cancellationToken = default);
}
