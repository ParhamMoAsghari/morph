using FluentAssertions;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class CompositionTests
{
    [Fact]
    public async Task Compose_employee_from_person_and_employment_flattens_deterministically()
    {
        var fx = new EntityEngineFixture();
        var fullNameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var emailId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var titleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var startedId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var numberId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var (_, personV) = await fx.CreatePublishedComponentAsync(
            "person",
            EntityEngineFixture.Field("fullName", fieldId: fullNameId),
            EntityEngineFixture.Field("email", fieldId: emailId));

        var (_, employmentV) = await fx.CreatePublishedComponentAsync(
            "employment-details",
            EntityEngineFixture.Field("title", fieldId: titleId),
            EntityEngineFixture.Field("startedOn", FieldDataType.DateTime, startedId));

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
                    TargetDefinitionId = personV.DefinitionId,
                    PinnedVersionId = personV.VersionId,
                    Mode = ComponentIncludeMode.Embed
                },
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "employment",
                    TargetDefinitionId = employmentV.DefinitionId,
                    PinnedVersionId = employmentV.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ],
            [EntityEngineFixture.Field("employeeNumber", fieldId: numberId, required: true)]);

        compose.IsSuccess.Should().BeTrue(compose.Error?.Message);

        var published = await fx.Service.PublishAsync(compose.Value.Version.VersionId);
        published.IsSuccess.Should().BeTrue(published.Error?.Message);

        var model = published.Value.EffectiveModel!;
        model.Fields.Select(f => f.Path).Should().BeEquivalentTo(
            ["employeeNumber", "employment.startedOn", "employment.title", "person.email", "person.fullName"],
            opts => opts.WithStrictOrdering());

        model.Fields.Single(f => f.FieldId == emailId).Path.Should().Be("person.email");
        model.PinnedDependencies.Should().HaveCount(2);
        model.Fingerprint.Should().NotBeNullOrWhiteSpace();

        var recompiled = await fx.Compiler.CompileAsync(compose.Value.Version);
        recompiled.IsSuccess.Should().BeTrue();
        recompiled.Value.Fingerprint.Should().Be(model.Fingerprint);
    }

    [Fact]
    public async Task Diamond_same_FieldId_merges_without_conflict()
    {
        var fx = new EntityEngineFixture();
        var sharedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var (_, sharedV) = await fx.CreatePublishedComponentAsync(
            "shared",
            EntityEngineFixture.Field("label", fieldId: sharedId));

        var leftCompose = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "left",
            "Left",
            DefinitionKind.Component,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "shared",
                    TargetDefinitionId = sharedV.DefinitionId,
                    PinnedVersionId = sharedV.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ]);
        leftCompose.IsSuccess.Should().BeTrue(leftCompose.Error?.Message);
        var leftPub = await fx.Service.PublishAsync(leftCompose.Value.Version.VersionId);
        leftPub.IsSuccess.Should().BeTrue(leftPub.Error?.Message);

        var rightCompose = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "right",
            "Right",
            DefinitionKind.Component,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "shared",
                    TargetDefinitionId = sharedV.DefinitionId,
                    PinnedVersionId = sharedV.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ]);
        rightCompose.IsSuccess.Should().BeTrue(rightCompose.Error?.Message);
        var rightPub = await fx.Service.PublishAsync(rightCompose.Value.Version.VersionId);
        rightPub.IsSuccess.Should().BeTrue(rightPub.Error?.Message);

        var root = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "root",
            "Root",
            DefinitionKind.Entity,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "left",
                    TargetDefinitionId = leftPub.Value.DefinitionId,
                    PinnedVersionId = leftPub.Value.VersionId,
                    Mode = ComponentIncludeMode.Embed
                },
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "right",
                    TargetDefinitionId = rightPub.Value.DefinitionId,
                    PinnedVersionId = rightPub.Value.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ]);
        root.IsSuccess.Should().BeTrue(root.Error?.Message);

        var compiled = await fx.Service.CompileAsync(root.Value.Version.VersionId);
        compiled.IsSuccess.Should().BeTrue(compiled.Error?.Message);
        compiled.Value.Fields.Where(f => f.FieldId == sharedId).Should().ContainSingle();
        compiled.Value.Fields.Single(f => f.FieldId == sharedId).SourcePaths.Should().HaveCount(2);
    }

    [Fact]
    public async Task Diamond_distinct_FieldIds_same_path_requires_explicit_resolution()
    {
        var fx = new EntityEngineFixture();
        var embeddedEmail = Guid.NewGuid();
        var localEmail = Guid.NewGuid();

        var (_, contactV) = await fx.CreatePublishedComponentAsync(
            "contact",
            EntityEngineFixture.Field("email", fieldId: embeddedEmail));

        // Local slug "contact.email" normalizes to the same path as Embed(alias=contact).email.
        var holder = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "holder",
            "Holder",
            DefinitionKind.Entity,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "contact",
                    TargetDefinitionId = contactV.DefinitionId,
                    PinnedVersionId = contactV.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ],
            [EntityEngineFixture.Field("contact.email", fieldId: localEmail)]);

        holder.IsSuccess.Should().BeTrue(holder.Error?.Message);
        var conflictCompile = await fx.Service.CompileAsync(holder.Value.Version.VersionId);
        conflictCompile.IsFailure.Should().BeTrue();
        conflictCompile.Error!.Message.Should().Contain("diamond_conflict");

        var resolved = await fx.Service.UpdateDraftAsync(holder.Value.Version.VersionId, draft =>
        {
            draft.ConflictResolutions =
            [
                new ConflictResolution { ConflictPath = "contact.email", WinningFieldId = localEmail }
            ];
        });
        resolved.IsSuccess.Should().BeTrue();

        var ok = await fx.Service.CompileAsync(holder.Value.Version.VersionId);
        ok.IsSuccess.Should().BeTrue(ok.Error?.Message);
        ok.Value.Fields.Should().ContainSingle(f => f.Path == "contact.email");
        ok.Value.Fields.Single(f => f.Path == "contact.email").FieldId.Should().Be(localEmail);
        ok.Value.Fields.Should().NotContain(f => f.FieldId == embeddedEmail);
    }

    [Fact]
    public async Task Reference_mode_does_not_flatten_child_fields()
    {
        var fx = new EntityEngineFixture();
        var (_, orgV) = await fx.CreatePublishedEntityAsync(
            "organization",
            EntityEngineFixture.Field("name"));

        var compose = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "membership",
            "Membership",
            DefinitionKind.Entity,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Alias = "organization",
                    TargetDefinitionId = orgV.DefinitionId,
                    PinnedVersionId = orgV.VersionId,
                    Mode = ComponentIncludeMode.Reference
                }
            ],
            [EntityEngineFixture.Field("role")]);

        compose.IsSuccess.Should().BeTrue(compose.Error?.Message);
        var model = await fx.Service.CompileAsync(compose.Value.Version.VersionId);
        model.IsSuccess.Should().BeTrue(model.Error?.Message);
        model.Value.Fields.Select(f => f.Path).Should().BeEquivalentTo(["organization", "role"]);
        model.Value.Fields.Single(f => f.Path == "organization").DataType.Should().Be(FieldDataType.Guid);
        model.Value.Fields.Should().NotContain(f => f.Path.Contains("name"));
    }
}
