namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class EntityDefinitionRecord
{
    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required string Kind { get; set; }

    public Guid? CurrentPublishedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
