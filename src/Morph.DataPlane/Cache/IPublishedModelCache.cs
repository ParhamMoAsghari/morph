namespace Morph.DataPlane.Cache;

public interface IPublishedModelCache
{
    Task<PublishedEntityModel?> GetBySlugAsync(
        Guid environmentId,
        string entitySlug,
        CancellationToken cancellationToken = default);

    void InvalidateEnvironment(Guid environmentId);

    void InvalidateDefinition(Guid environmentId, Guid definitionId);

    void InvalidateAll();
}
