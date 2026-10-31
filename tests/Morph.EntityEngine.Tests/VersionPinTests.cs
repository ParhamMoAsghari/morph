using FluentAssertions;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class VersionPinTests
{
    [Fact]
    public async Task Publishing_person_v2_does_not_mutate_employee_pin()
    {
        var fx = new EntityEngineFixture();
        var emailId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var phoneId = Guid.Parse("34343434-3434-3434-3434-343434343434");

        var (personDef, personV1) = await fx.CreatePublishedComponentAsync(
            "person",
            EntityEngineFixture.Field("email", fieldId: emailId));

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
                    TargetDefinitionId = personDef.DefinitionId,
                    PinnedVersionId = personV1.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ],
            [EntityEngineFixture.Field("employeeNumber")]);
        compose.IsSuccess.Should().BeTrue(compose.Error?.Message);

        var employeePub = await fx.Service.PublishAsync(compose.Value.Version.VersionId);
        employeePub.IsSuccess.Should().BeTrue(employeePub.Error?.Message);
        var fingerprintBefore = employeePub.Value.EffectiveModel!.Fingerprint;

        // Evolve Person → v2 with phone; Employee must remain on v1 until explicit upgrade.
        var personDraft = await fx.Service.CreateDraftFromPublishedAsync(personV1.VersionId);
        personDraft.IsSuccess.Should().BeTrue(personDraft.Error?.Message);
        await fx.Service.UpdateDraftAsync(personDraft.Value.VersionId, draft =>
        {
            draft.Fields.Add(EntityEngineFixture.Field("phone", fieldId: phoneId));
        });
        var personV2 = await fx.Service.PublishAsync(personDraft.Value.VersionId);
        personV2.IsSuccess.Should().BeTrue(personV2.Error?.Message);

        var employeeStill = await fx.Service.GetVersionAsync(employeePub.Value.VersionId);
        employeeStill!.ComponentUses.Single().PinnedVersionId.Should().Be(personV1.VersionId);
        employeeStill.EffectiveModel!.Fingerprint.Should().Be(fingerprintBefore);
        employeeStill.EffectiveModel.Fields.Should().NotContain(f => f.FieldId == phoneId);

        // Explicit upgrade path
        var employeeDraft = await fx.Service.CreateDraftFromPublishedAsync(employeePub.Value.VersionId);
        employeeDraft.IsSuccess.Should().BeTrue(employeeDraft.Error?.Message);
        var useId = employeeDraft.Value.ComponentUses.Single().ComponentUseId;
        var upgraded = await fx.Service.UpgradePinAsync(
            employeeDraft.Value.VersionId,
            useId,
            personV2.Value.VersionId);
        upgraded.IsSuccess.Should().BeTrue(upgraded.Error?.Message);

        var upgradedPub = await fx.Service.PublishAsync(employeeDraft.Value.VersionId);
        upgradedPub.IsSuccess.Should().BeTrue(upgradedPub.Error?.Message);
        upgradedPub.Value.EffectiveModel!.Fields.Should().Contain(f => f.FieldId == phoneId);
        upgradedPub.Value.EffectiveModel.Fingerprint.Should().NotBe(fingerprintBefore);
    }

    [Fact]
    public async Task Publish_requires_dependencies_to_be_published()
    {
        var fx = new EntityEngineFixture();
        var draftDep = await fx.Service.CreateAsync(
            fx.EnvironmentId,
            "dep",
            "Dep",
            DefinitionKind.Component,
            [EntityEngineFixture.Field("x")]);
        draftDep.IsSuccess.Should().BeTrue();

        var compose = await fx.Service.ComposeAsync(
            fx.EnvironmentId,
            "parent",
            "Parent",
            DefinitionKind.Entity,
            [
                new ComponentUse
                {
                    ComponentUseId = Guid.NewGuid(),
                    Alias = "dep",
                    TargetDefinitionId = draftDep.Value.Definition.DefinitionId,
                    PinnedVersionId = draftDep.Value.Version.VersionId,
                    Mode = ComponentIncludeMode.Embed
                }
            ]);
        compose.IsSuccess.Should().BeTrue();

        var publish = await fx.Service.PublishAsync(compose.Value.Version.VersionId);
        publish.IsFailure.Should().BeTrue();
        publish.Error!.Message.Should().Contain("not published");
    }

    [Fact]
    public async Task Published_version_is_immutable()
    {
        var fx = new EntityEngineFixture();
        var (_, published) = await fx.CreatePublishedEntityAsync(
            "imm",
            EntityEngineFixture.Field("a"));

        var update = await fx.Service.UpdateDraftAsync(published.VersionId, draft =>
        {
            draft.Fields.Add(EntityEngineFixture.Field("b"));
        });

        update.IsFailure.Should().BeTrue();
        update.Error!.Code.Should().Be("conflict");
    }
}
