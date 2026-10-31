namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class StorageMappingRecord
{
    public Guid VersionId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string SchemaName { get; set; }

    public required string TableName { get; set; }

    public required string Fingerprint { get; set; }

    public required string MappingJson { get; set; }

    public DateTimeOffset MappedAt { get; set; }
}
