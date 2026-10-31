using FluentAssertions;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Services;
using Morph.EntityEngine.Storage;
using Morph.EntityEngine.Validation;
using Morph.Infrastructure.Persistence.EntityEngine;
using Morph.Infrastructure.Storage;
using Xunit;

namespace Morph.EntityEngine.Integration.Tests;

[Collection("postgres")]
public sealed class StagedMigrationIntegrationTests
{
    private readonly PostgresFixture _postgres;

    public StagedMigrationIntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Create_table_migration_persists_definitions_and_creates_hybrid_table()
    {
        await using var db = _postgres.CreateDbContext();
        var harness = CreateHarness(db);

        var envId = Guid.NewGuid();
        var schema = "p_it_create_" + Guid.NewGuid().ToString("N")[..8];

        var title = Field("title", required: true);
        title.IsIndexed = true;
        var create = await harness.Service.CreateAsync(
            envId,
            "invoice",
            "Invoice",
            DefinitionKind.Entity,
            [title, Field("notes")]);
        create.IsSuccess.Should().BeTrue(create.Error?.Message);

        var publish = await harness.Service.PublishAsync(create.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue(publish.Error?.Message);

        var definitionBefore = await harness.Definitions.GetDefinitionAsync(create.Value.Definition.DefinitionId);
        definitionBefore!.CurrentPublishedVersionId.Should().BeNull(
            "entity pointer advances only on migration swap");

        var planResult = await harness.Migrations.PlanCreateAsync(publish.Value.VersionId, schema);
        planResult.IsSuccess.Should().BeTrue(planResult.Error?.Message);

        var apply = await harness.Migrations.ApplyAsync(planResult.Value.Plan);
        apply.IsSuccess.Should().BeTrue(apply.Error?.Message);
        apply.Value.Status.Should().Be(MigrationRunStatus.Completed);

        var definitionAfter = await harness.Definitions.GetDefinitionAsync(create.Value.Definition.DefinitionId);
        definitionAfter!.CurrentPublishedVersionId.Should().Be(publish.Value.VersionId);

        var mapping = planResult.Value.Mapping;
        var ddl = new NpgsqlDdlExecutor(db);
        (await ddl.TableExistsAsync(mapping.SchemaName, mapping.TableName)).Should().BeTrue();
        (await ddl.ColumnExistsAsync(mapping.SchemaName, mapping.TableName, "_extras")).Should().BeTrue();
        (await ddl.ColumnExistsAsync(mapping.SchemaName, mapping.TableName, "definition_version_id")).Should().BeTrue();

        var stored = await harness.Mappings.GetByVersionAsync(publish.Value.VersionId);
        stored.Should().NotBeNull();
        stored!.Fingerprint.Should().Be(mapping.Fingerprint);

        var reloadedVersion = await harness.Definitions.GetVersionAsync(publish.Value.VersionId);
        reloadedVersion.Should().NotBeNull();
        reloadedVersion!.EffectiveModel.Should().NotBeNull();
        reloadedVersion.Fields.Should().HaveCount(2);
    }

    [Fact]
    public async Task Mid_fail_leaves_prior_pointer_and_resume_completes()
    {
        await using var db = _postgres.CreateDbContext();
        var harness = CreateHarness(db);

        var envId = Guid.NewGuid();
        var schema = "p_it_fail_" + Guid.NewGuid().ToString("N")[..8];

        // v1
        var createV1 = await harness.Service.CreateAsync(
            envId,
            "device",
            "Device",
            DefinitionKind.Entity,
            [Field("serial", required: true)]);
        createV1.IsSuccess.Should().BeTrue(createV1.Error?.Message);
        var publishV1 = await harness.Service.PublishAsync(createV1.Value.Version.VersionId);
        publishV1.IsSuccess.Should().BeTrue(publishV1.Error?.Message);

        var planV1 = await harness.Migrations.PlanCreateAsync(publishV1.Value.VersionId, schema);
        planV1.IsSuccess.Should().BeTrue(planV1.Error?.Message);
        var applyV1 = await harness.Migrations.ApplyAsync(planV1.Value.Plan);
        applyV1.IsSuccess.Should().BeTrue(applyV1.Error?.Message);

        var definition = await harness.Definitions.GetDefinitionAsync(createV1.Value.Definition.DefinitionId);
        definition!.CurrentPublishedVersionId.Should().Be(publishV1.Value.VersionId);
        var priorPointer = definition.CurrentPublishedVersionId;

        // v2 draft + publish
        var draft = await harness.Service.CreateDraftFromPublishedAsync(publishV1.Value.VersionId);
        draft.IsSuccess.Should().BeTrue(draft.Error?.Message);
        draft.Value.Fields.Add(Field("firmware"));
        await harness.Definitions.SaveVersionAsync(draft.Value);

        var publishV2 = await harness.Service.PublishAsync(draft.Value.VersionId);
        publishV2.IsSuccess.Should().BeTrue(publishV2.Error?.Message);

        definition = await harness.Definitions.GetDefinitionAsync(createV1.Value.Definition.DefinitionId);
        definition!.CurrentPublishedVersionId.Should().Be(priorPointer, "publish must not advance entity pointer");

        var planV2 = await harness.Migrations.PlanEvolveAsync(
            publishV1.Value.VersionId,
            publishV2.Value.VersionId,
            schema);
        planV2.IsSuccess.Should().BeTrue(planV2.Error?.Message);

        var swapOrdinal = planV2.Value.Plan.Steps
            .Single(s => s.Kind == MigrationStepKind.SwapPointer)
            .Ordinal;

        harness.Executor.BeforeStepAsync = (run, step, _) =>
        {
            if (step.Ordinal == swapOrdinal)
            {
                throw new InvalidOperationException("simulated mid-migration failure before swap");
            }

            return Task.CompletedTask;
        };

        var failed = await harness.Migrations.ApplyAsync(planV2.Value.Plan, priorPointer);
        failed.IsSuccess.Should().BeFalse();
        failed.Error.Should().NotBeNull();

        var runs = await harness.Runs.ListByDefinitionAsync(createV1.Value.Definition.DefinitionId);
        var failedRun = runs.Last(r => r.ToVersionId == publishV2.Value.VersionId);
        failedRun.Status.Should().Be(MigrationRunStatus.Failed);
        failedRun.LastCompletedStepOrdinal.Should().BeLessThan(swapOrdinal);

        definition = await harness.Definitions.GetDefinitionAsync(createV1.Value.Definition.DefinitionId);
        definition!.CurrentPublishedVersionId.Should().Be(priorPointer);

        harness.Executor.BeforeStepAsync = null;
        var resumed = await harness.Migrations.ResumeAsync(failedRun.RunId);
        resumed.IsSuccess.Should().BeTrue(resumed.Error?.Message);
        resumed.Value.Status.Should().Be(MigrationRunStatus.Completed);

        definition = await harness.Definitions.GetDefinitionAsync(createV1.Value.Definition.DefinitionId);
        definition!.CurrentPublishedVersionId.Should().Be(publishV2.Value.VersionId);

        var ddl = new NpgsqlDdlExecutor(db);
        var mapping = planV2.Value.Mapping;
        var firmwareCol = mapping.Columns.Single(c => c.FieldPath == "firmware" || c.Kind == StorageColumnKind.ExtrasJsonb);
        // firmware is unindexed optional → extras; ensure table still exists
        (await ddl.TableExistsAsync(mapping.SchemaName, mapping.TableName)).Should().BeTrue();
        _ = firmwareCol;
    }

    private static FieldDefinition Field(
        string slug,
        FieldDataType type = FieldDataType.String,
        bool required = false) =>
        new()
        {
            FieldId = Guid.NewGuid(),
            Slug = slug,
            DisplayName = slug,
            DataType = type,
            IsRequired = required
        };

    private static Harness CreateHarness(Morph.Infrastructure.Persistence.PlatformDbContext db)
    {
        var clock = new SystemClock();
        var definitions = new EfDefinitionStore(db);
        var runs = new EfMigrationRunStore(db);
        var mappings = new EfStorageMappingStore(db);
        var ddl = new NpgsqlDdlExecutor(db);

        var validator = new DefinitionGraphValidator(definitions);
        var compiler = new EffectiveModelCompiler(definitions, validator, clock);
        var composition = new CompositionOperations(definitions, clock);
        var decomposition = new DecompositionOperations(definitions, clock);
        var publisher = new DefinitionPublisher(definitions, compiler, clock);
        var service = new EntityDefinitionService(
            definitions,
            validator,
            compiler,
            composition,
            decomposition,
            publisher,
            clock);

        var mapper = new StorageMapper(clock);
        var planner = new MigrationPlanner(clock);
        var executor = new StagedMigrationExecutor(ddl, runs, definitions, mappings, clock);
        var migrations = new MigrationService(definitions, mapper, planner, executor, mappings);

        return new Harness(definitions, runs, mappings, service, migrations, executor);
    }

    private sealed record Harness(
        EfDefinitionStore Definitions,
        EfMigrationRunStore Runs,
        EfStorageMappingStore Mappings,
        EntityDefinitionService Service,
        MigrationService Migrations,
        StagedMigrationExecutor Executor);
}
