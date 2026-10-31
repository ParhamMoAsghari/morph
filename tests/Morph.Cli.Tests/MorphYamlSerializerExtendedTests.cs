using FluentAssertions;
using Morph.Cli.Schema;
using Xunit;

namespace Morph.Cli.Tests;

public sealed class MorphYamlSerializerExtendedTests
{
    [Fact]
    public void RoleIr_and_RolesIr_yaml_round_trip()
    {
        var roles = new RolesIr
        {
            Roles =
            [
                new RoleIr { Name = "viewer", DisplayName = "Viewer", Inherits = ["employee.reader"] },
                new RoleIr { Name = "admin", DisplayName = "Admin" }
            ]
        };

        var yaml = MorphYamlSerializer.Serialize(roles);
        var loaded = MorphYamlSerializer.Deserialize<RolesIr>(yaml);
        loaded.Roles.Should().HaveCount(2);
        loaded.Roles[0].Inherits.Should().ContainSingle("employee.reader");
    }

    [Fact]
    public void ConflictResolutionIr_round_trips_in_definition()
    {
        var winId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var ir = new DefinitionIr
        {
            Slug = "composed",
            DisplayName = "Composed",
            ConflictResolutions =
            [
                new ConflictResolutionIr { ConflictPath = "person.email", WinningFieldId = winId }
            ]
        };

        var loaded = MorphYamlSerializer.Deserialize<DefinitionIr>(MorphYamlSerializer.Serialize(ir));
        loaded.ConflictResolutions.Should().ContainSingle(c =>
            c.ConflictPath == "person.email" && c.WinningFieldId == winId);
        MorphYamlSerializer.Canonicalize(loaded).Should().Contain("person.email");
    }

    [Fact]
    public void Save_and_load_definition_on_disk_assigns_field_ids()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "morph", "definitions", "widget.entity.yaml");
        var ir = new DefinitionIr
        {
            Slug = "widget",
            Fields = [new FieldIr { Slug = "code", DataType = "String" }]
        };

        MorphYamlSerializer.SaveDefinition(path, ir);
        var loaded = MorphYamlSerializer.LoadDefinition(path);
        loaded.DisplayName.Should().Be("widget");
        loaded.Fields.Should().ContainSingle(f => f.Slug == "code" && f.FieldId != null);
    }

    [Fact]
    public void LoadDefinition_without_slug_throws()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "bad.yaml");
        File.WriteAllText(path, "kind: Entity\nfields: []");

        var act = () => MorphYamlSerializer.LoadDefinition(path);
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing slug*");
    }

    [Fact]
    public void Save_and_load_policy_on_disk()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "morph", "policies", "p.policy.yaml");
        var ir = new PolicyIr
        {
            Name = "demo.policy",
            Actions = ["read"],
            Resources = ["entity:*"]
        };

        MorphYamlSerializer.SavePolicy(path, ir);
        MorphYamlSerializer.LoadPolicy(path).Name.Should().Be("demo.policy");
    }

    [Fact]
    public void LoadPolicy_without_name_throws()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "bad.policy.yaml");
        File.WriteAllText(path, "kind: Policy\nactions: []\nresources: []");

        var act = () => MorphYamlSerializer.LoadPolicy(path);
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing name*");
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "morph-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
