namespace Morph.DataPlane.Cache;

public interface IEnvironmentSchemaResolver
{
    Task<EnvironmentSchemaInfo?> ResolveAsync(Guid environmentId, CancellationToken cancellationToken = default);
}

public sealed record EnvironmentSchemaInfo(Guid EnvironmentId, Guid ProjectId, string SchemaName);
