using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.BuildingBlocks.Time;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Query;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Storage;
using Morph.StorageEngine.DependencyInjection;
using Morph.StorageEngine.Ports;
using Morph.StorageEngine.Services;
using Xunit;

namespace Morph.StorageEngine.Tests;

public sealed class StorageEngineFacadeTests
{
    [Fact]
    public void Describe_postgres_uses_environment_shard_key()
    {
        var engine = new StorageEngineFacade(StorageBackendKind.Postgres);
        var d = engine.Describe();
        d.Backend.Should().Be(StorageBackendKind.Postgres);
        d.ShardKey.Should().Contain("environment_id");
        d.Notes.Should().Contain("Postgres");
    }

    [Fact]
    public void Describe_citus_uses_project_shard_key()
    {
        var engine = new StorageEngineFacade(StorageBackendKind.Citus);
        var d = engine.Describe();
        d.Backend.Should().Be(StorageBackendKind.Citus);
        d.ShardKey.Should().Be("project_id");
        d.Notes.Should().Contain("Citus");
    }

    [Fact]
    public void CompileFilter_null_returns_TRUE()
    {
        var engine = new StorageEngineFacade();
        var model = BuildModel();
        var result = engine.CompileFilter(model, null);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Sql.Should().Be("TRUE");
        result.Value.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void CompileFilter_comparison_on_system_field()
    {
        var engine = new StorageEngineFacade();
        var model = BuildModel();
        var filter = new FilterComparison("id", FilterOperator.Eq, Guid.NewGuid());
        var result = engine.CompileFilter(model, filter);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Sql.Should().Contain("id");
        result.Value.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void CompileFilter_and_or_not_tree()
    {
        var engine = new StorageEngineFacade();
        var model = BuildModel();
        var filter = new FilterAnd(
        [
            new FilterOr(
            [
                new FilterComparison("created_at", FilterOperator.Gte, DateTimeOffset.UtcNow.AddDays(-1)),
                new FilterComparison("owner_id", FilterOperator.IsNotNull, null)
            ]),
            new FilterNot(new FilterComparison("id", FilterOperator.Eq, Guid.Empty))
        ]);

        var result = engine.CompileFilter(model, filter);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Sql.Should().Contain("AND");
        result.Value.Sql.Should().Contain("OR");
    }

    [Fact]
    public void CompileFilter_unknown_field_fails()
    {
        var engine = new StorageEngineFacade();
        var model = BuildModel();
        var result = engine.CompileFilter(
            model,
            new FilterComparison("nope.field", FilterOperator.Eq, "x"));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompileFilter_null_model_throws()
    {
        var engine = new StorageEngineFacade();
        var act = () => engine.CompileFilter(null!, null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DI_registers_singleton_engine()
    {
        var services = new ServiceCollection();
        services.AddMorphStorageEngine(StorageBackendKind.Citus);
        using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IStorageEngine>();
        engine.Backend.Should().Be(StorageBackendKind.Citus);
        ReferenceEquals(engine, sp.GetRequiredService<IStorageEngine>()).Should().BeTrue();
    }

    private static PublishedEntityModel BuildModel()
    {
        var envId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        var effective = new EffectiveModel
        {
            DefinitionId = defId,
            VersionId = versionId,
            EnvironmentId = envId,
            Slug = "note",
            Kind = DefinitionKind.Entity,
            VersionNumber = 1,
            Fields =
            [
                new EffectiveField
                {
                    FieldId = fieldId,
                    Path = "title",
                    Slug = "title",
                    DisplayName = "Title",
                    DataType = FieldDataType.String,
                    IsRequired = true,
                    IsIndexed = true,
                    SourceDefinitionId = defId,
                    SourceVersionId = versionId,
                    SourcePaths = ["title"]
                }
            ],
            Components = [],
            Relationships = [],
            PinnedDependencies = [],
            CompiledAt = DateTimeOffset.UtcNow,
            Fingerprint = "test"
        };

        var mapping = new StorageMapper(new SystemClock()).Map(effective, "public");

        return new PublishedEntityModel
        {
            EnvironmentId = envId,
            ProjectId = Guid.NewGuid(),
            DefinitionId = defId,
            Slug = "note",
            Version = new DefinitionVersion
            {
                VersionId = versionId,
                DefinitionId = defId,
                EnvironmentId = envId,
                Slug = "note",
                DisplayName = "Note",
                Kind = DefinitionKind.Entity,
                VersionNumber = 1,
                Status = DefinitionVersionStatus.Published,
                Fields =
                [
                    new FieldDefinition
                    {
                        FieldId = fieldId,
                        Slug = "title",
                        DisplayName = "Title",
                        DataType = FieldDataType.String,
                        IsRequired = true,
                        IsIndexed = true
                    }
                ],
                ComponentUses = [],
                Relationships = [],
                ConflictResolutions = [],
                CreatedAt = DateTimeOffset.UtcNow,
                PublishedAt = DateTimeOffset.UtcNow,
                EffectiveModel = effective
            },
            EffectiveModel = effective,
            StorageMapping = mapping,
            SchemaName = "public",
            LoadedAt = DateTimeOffset.UtcNow
        };
    }
}
