using Morph.EntityEngine.Domain;

namespace Morph.DataPlane.Cache;

public sealed class PublishedEntityModel
{
    public required Guid EnvironmentId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid DefinitionId { get; init; }

    public required string Slug { get; init; }

    public required DefinitionVersion Version { get; init; }

    public required EffectiveModel EffectiveModel { get; init; }

    public required StorageMapping StorageMapping { get; init; }

    public required string SchemaName { get; init; }

    public DateTimeOffset LoadedAt { get; init; }
}
