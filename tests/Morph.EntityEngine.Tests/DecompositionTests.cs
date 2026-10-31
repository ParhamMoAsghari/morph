using FluentAssertions;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class DecompositionTests
{
    [Fact]
    public async Task Decompose_preserves_FieldIds_and_rewires_embed()
    {
        var fx = new EntityEngineFixture();
        var nameId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var emailId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var codeId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var create = await fx.Service.CreateAsync(
            fx.EnvironmentId,
            "employee",
            "Employee",
            DefinitionKind.Entity,
            [
                EntityEngineFixture.Field("fullName", fieldId: nameId),
                EntityEngineFixture.Field("email", fieldId: emailId),
                EntityEngineFixture.Field("employeeNumber", fieldId: codeId)
            ]);
        create.IsSuccess.Should().BeTrue(create.Error?.Message);

        var result = await fx.Service.DecomposeAsync(
            create.Value.Version.VersionId,
            [nameId, emailId],
            "person",
            "Person",
            "person");

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ComponentDefinition.Kind.Should().Be(DefinitionKind.Component);
        result.Value.ComponentVersion.Fields.Select(f => f.FieldId)
            .Should().BeEquivalentTo([nameId, emailId]);

        var source = result.Value.UpdatedSourceVersion;
        source.Fields.Select(f => f.FieldId).Should().BeEquivalentTo([codeId]);
        source.ComponentUses.Should().ContainSingle(c =>
            c.Alias == "person"
            && c.PinnedVersionId == result.Value.ComponentVersion.VersionId
            && c.Mode == ComponentIncludeMode.Embed);

        // Publish component then parent; EffectiveModel should still expose person.email with same FieldId.
        var pubComponent = await fx.Service.PublishAsync(result.Value.ComponentVersion.VersionId);
        pubComponent.IsSuccess.Should().BeTrue(pubComponent.Error?.Message);

        var pubParent = await fx.Service.PublishAsync(source.VersionId);
        pubParent.IsSuccess.Should().BeTrue(pubParent.Error?.Message);

        var model = pubParent.Value.EffectiveModel!;
        model.Fields.Single(f => f.FieldId == emailId).Path.Should().Be("person.email");
        model.Fields.Single(f => f.FieldId == codeId).Path.Should().Be("employeeNumber");
    }

    [Fact]
    public async Task Decompose_fork_assigns_new_FieldIds()
    {
        var fx = new EntityEngineFixture();
        var originalId = Guid.NewGuid();
        var create = await fx.Service.CreateAsync(
            fx.EnvironmentId,
            "thing",
            "Thing",
            DefinitionKind.Entity,
            [EntityEngineFixture.Field("title", fieldId: originalId)]);
        create.IsSuccess.Should().BeTrue();

        var result = await fx.Service.DecomposeAsync(
            create.Value.Version.VersionId,
            [originalId],
            "title-block",
            "TitleBlock",
            "titleBlock",
            forkFields: true);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ComponentVersion.Fields.Should().ContainSingle();
        result.Value.ComponentVersion.Fields[0].FieldId.Should().NotBe(originalId);
        result.Value.UpdatedSourceVersion.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task Cannot_decompose_published_version()
    {
        var fx = new EntityEngineFixture();
        var (_, published) = await fx.CreatePublishedEntityAsync(
            "locked",
            EntityEngineFixture.Field("a"),
            EntityEngineFixture.Field("b"));

        var result = await fx.Service.DecomposeAsync(
            published.VersionId,
            [published.Fields[0].FieldId],
            "extracted",
            "Extracted",
            "extracted");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("conflict");
    }
}
