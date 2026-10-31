using FluentAssertions;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Storage;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class StorageMappingTests
{
    [Fact]
    public async Task Map_hybrid_entity_uses_typed_columns_embed_jsonb_and_extras()
    {
        var fx = new EntityEngineFixture();
        var (_, person) = await fx.CreatePublishedComponentAsync(
            "person",
            EntityEngineFixture.Field("fullName"),
            EntityEngineFixture.Field("email"));

        var employeeNumber = EntityEngineFixture.Field("employeeNumber", required: true);
        employeeNumber.IsIndexed = true;

        var compose = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "employee",
            "Employee",
            DefinitionKind.Entity,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "person",
                    TargetDefinitionId = person.DefinitionId,
                    PinnedVersionId = person.VersionId,
                    Mode = ComponentIncludeMode.Embed,
                    IsRequired = true
                }
            ],
            [
                employeeNumber,
                EntityEngineFixture.Field("nickname")
            ]);
        compose.IsSuccess.Should().BeTrue(compose.Error?.Message);

        var publish = await fx.Service.PublishAsync(compose.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue(publish.Error?.Message);

        var mapper = new StorageMapper(new SystemClock());
        var mapping = mapper.Map(publish.Value.EffectiveModel!, "p_demo_dev");

        mapping.SchemaName.Should().Be("p_demo_dev");
        mapping.TableName.Should().Be("e_employee");
        mapping.Columns.Should().Contain(c => c.ColumnName == "id" && c.IsPrimaryKey);
        mapping.Columns.Should().Contain(c =>
            c.Kind == StorageColumnKind.EmbedJsonb && c.ComponentAlias == "person");
        mapping.Columns.Should().Contain(c =>
            c.Kind == StorageColumnKind.TypedField && c.FieldPath == "employeeNumber");
        mapping.Columns.Should().Contain(c => c.Kind == StorageColumnKind.ExtrasJsonb);
        mapping.Fingerprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlanCreate_is_staged_expand_backfill_constrain_swap()
    {
        var fx = new EntityEngineFixture();
        var title = EntityEngineFixture.Field("title", required: true);
        title.IsIndexed = true;
        var (_, ver) = await fx.CreatePublishedEntityAsync("ticket", title);

        var mapper = new StorageMapper(fx.Clock);
        var planner = new MigrationPlanner(fx.Clock);
        var mapping = mapper.Map(ver.EffectiveModel!, "app_test");
        var plan = planner.PlanCreate(mapping);

        plan.Steps.Select(s => s.Stage).Distinct().Should().ContainInOrder(
            MigrationStage.Validate,
            MigrationStage.Expand,
            MigrationStage.Backfill,
            MigrationStage.Constrain,
            MigrationStage.Swap);

        plan.Steps.Should().Contain(s =>
            s.Kind == MigrationStepKind.CreateTable
            && s.Sql.Contains("CREATE TABLE", StringComparison.Ordinal));
        plan.Steps.Should().Contain(s => s.Kind == MigrationStepKind.SwapPointer);
        plan.RiskClass.Should().Be(MigrationRiskClass.Additive);
    }
}
