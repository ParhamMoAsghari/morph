using FluentAssertions;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class ValidationTests
{
    [Fact]
    public async Task Rejects_composition_cycles()
    {
        var fx = new EntityEngineFixture();

        var aCreate = await fx.Service.CreateAsync(fx.EnvironmentId, "a", "A", DefinitionKind.Component);
        aCreate.IsSuccess.Should().BeTrue();
        var bCreate = await fx.Service.CreateAsync(fx.EnvironmentId, "b", "B", DefinitionKind.Component);
        bCreate.IsSuccess.Should().BeTrue();

        // A embeds B (draft pin allowed for graph validation of cycles)
        await fx.Service.UpdateDraftAsync(aCreate.Value.Version.VersionId, draft =>
        {
            draft.ComponentUses.Add(new ComponentUse
            {
                ComponentUseId = Guid.NewGuid(),
                Alias = "b",
                TargetDefinitionId = bCreate.Value.Definition.DefinitionId,
                PinnedVersionId = bCreate.Value.Version.VersionId,
                Mode = ComponentIncludeMode.Embed
            });
        });

        await fx.Service.UpdateDraftAsync(bCreate.Value.Version.VersionId, draft =>
        {
            draft.ComponentUses.Add(new ComponentUse
            {
                ComponentUseId = Guid.NewGuid(),
                Alias = "a",
                TargetDefinitionId = aCreate.Value.Definition.DefinitionId,
                PinnedVersionId = aCreate.Value.Version.VersionId,
                Mode = ComponentIncludeMode.Embed
            });
        });

        var issues = await fx.Service.ValidateAsync(aCreate.Value.Version.VersionId);
        issues.Should().Contain(i => i.Code == "composition_cycle");
    }

    [Fact]
    public async Task Rejects_composition_depth_greater_than_five()
    {
        var fx = new EntityEngineFixture();
        DefinitionVersion? previous = null;

        for (var i = 0; i <= EntityEngineLimits.MaxCompositionDepth + 1; i++)
        {
            var slug = $"c{i}";
            var create = await fx.Service.CreateAsync(
                fx.EnvironmentId,
                slug,
                slug,
                DefinitionKind.Component,
                [EntityEngineFixture.Field($"f{i}")]);
            create.IsSuccess.Should().BeTrue(create.Error?.Message);

            if (previous is not null)
            {
                // Need previous published to satisfy publish chain; for depth check, pin drafts in a chain.
                await fx.Service.UpdateDraftAsync(create.Value.Version.VersionId, draft =>
                {
                    draft.ComponentUses.Add(new ComponentUse
                    {
                        ComponentUseId = Guid.NewGuid(),
                        Alias = "child",
                        TargetDefinitionId = previous.DefinitionId,
                        PinnedVersionId = previous.VersionId,
                        Mode = ComponentIncludeMode.Embed
                    });
                });
            }

            previous = (await fx.Store.GetVersionAsync(create.Value.Version.VersionId))!;
        }

        var deepest = previous!;
        var issues = await fx.Service.ValidateAsync(deepest.VersionId);
        issues.Should().Contain(i => i.Code == "composition_depth_exceeded");
    }

    [Fact]
    public async Task Rejects_duplicate_FieldIds_and_slugs_in_authoring()
    {
        var fx = new EntityEngineFixture();
        var id = Guid.NewGuid();
        var create = await fx.Service.CreateAsync(
            fx.EnvironmentId,
            "dup",
            "Dup",
            DefinitionKind.Entity,
            [
                EntityEngineFixture.Field("a", fieldId: id),
                EntityEngineFixture.Field("b", fieldId: id)
            ]);
        create.IsSuccess.Should().BeTrue();

        var issues = await fx.Service.ValidateAsync(create.Value.Version.VersionId);
        issues.Should().Contain(i => i.Code == "duplicate_field_id");

        await fx.Service.UpdateDraftAsync(create.Value.Version.VersionId, draft =>
        {
            draft.Fields =
            [
                EntityEngineFixture.Field("same"),
                EntityEngineFixture.Field("same")
            ];
        });

        issues = await fx.Service.ValidateAsync(create.Value.Version.VersionId);
        issues.Should().Contain(i => i.Code == "duplicate_field_slug");
    }

    [Fact]
    public async Task Rejects_missing_version_pin()
    {
        var fx = new EntityEngineFixture();
        var create = await fx.Service.CreateAsync(fx.EnvironmentId, "x", "X", DefinitionKind.Entity);
        create.IsSuccess.Should().BeTrue();

        await fx.Service.UpdateDraftAsync(create.Value.Version.VersionId, draft =>
        {
            draft.ComponentUses.Add(new ComponentUse
            {
                ComponentUseId = Guid.NewGuid(),
                Alias = "missing",
                TargetDefinitionId = Guid.NewGuid(),
                PinnedVersionId = Guid.Empty,
                Mode = ComponentIncludeMode.Embed
            });
        });

        var issues = await fx.Service.ValidateAsync(create.Value.Version.VersionId);
        issues.Should().Contain(i => i.Code == "version_pin_required");
    }
}
