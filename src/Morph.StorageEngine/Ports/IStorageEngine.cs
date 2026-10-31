using Morph.BuildingBlocks.Results;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Mapping;
using Morph.DataPlane.Query;

namespace Morph.StorageEngine.Ports;

public enum StorageBackendKind
{
    /// <summary>Single-node or managed PostgreSQL (default).</summary>
    Postgres = 0,

    /// <summary>Citus distributed PostgreSQL — planned; shard by project_id.</summary>
    Citus = 1
}

/// <summary>
/// Productized storage/query surface over DataPlane + EntityEngine (Postgres default; Citus planned).
/// </summary>
public interface IStorageEngine
{
    StorageBackendKind Backend { get; }

    /// <summary>
    /// Describes the active backend and shard strategy (informational for operators/SDKs).
    /// </summary>
    StorageEngineDescriptor Describe();

    /// <summary>
    /// Compiles an allowlisted filter AST into parameterized SQL (DataPlane <see cref="FilterCompiler"/>).
    /// </summary>
    Result<CompiledSql> CompileFilter(PublishedEntityModel model, FilterNode? filter);
}

public sealed class StorageEngineDescriptor
{
    public required StorageBackendKind Backend { get; init; }

    public required string ShardKey { get; init; }

    public required string Notes { get; init; }
}
