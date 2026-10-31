using Morph.BuildingBlocks.Results;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Mapping;
using Morph.DataPlane.Query;
using Morph.StorageEngine.Ports;

namespace Morph.StorageEngine.Services;

/// <summary>
/// Facade over DataPlane query compile + EntityEngine mapping concepts.
/// Citus routing is deferred; Postgres is the default backend.
/// </summary>
public sealed class StorageEngineFacade : IStorageEngine
{
    public StorageEngineFacade(StorageBackendKind backend = StorageBackendKind.Postgres)
    {
        Backend = backend;
    }

    public StorageBackendKind Backend { get; }

    public StorageEngineDescriptor Describe() => new()
    {
        Backend = Backend,
        ShardKey = Backend == StorageBackendKind.Citus ? "project_id" : "environment_id (single-node)",
        Notes = Backend == StorageBackendKind.Citus
            ? "Citus coordinator holds platform metadata; workers shard tenant data by project_id. See deploy/citus."
            : "Default Postgres backend via Morph.DataPlane / EntityEngine hybrid mapping."
    };

    public Result<CompiledSql> CompileFilter(PublishedEntityModel model, FilterNode? filter)
    {
        ArgumentNullException.ThrowIfNull(model);

        var compiler = new FilterCompiler(new FieldColumnResolver(model.StorageMapping, model.EffectiveModel));
        var result = compiler.Compile(filter);
        if (result.IsFailure)
        {
            return Result.Failure<CompiledSql>(result.Error!);
        }

        var (sql, parameters) = result.Value;
        return Result.Success(new CompiledSql
        {
            Sql = sql,
            Parameters = parameters
        });
    }
}
