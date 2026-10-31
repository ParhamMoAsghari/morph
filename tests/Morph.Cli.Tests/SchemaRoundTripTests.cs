using FluentAssertions;
using Morph.Cli.Codegen;
using Morph.Cli.Commands;
using Morph.Cli.Schema;
using Morph.Client.Models;
using Xunit;

namespace Morph.Cli.Tests;

[Collection("CliWorkingDirectory")]
public sealed class SchemaRoundTripTests
{
    [Fact]
    public void Definition_yaml_round_trips_fields_and_component_uses()
    {
        var original = new DefinitionIr
        {
            Kind = "Entity",
            Slug = "employee",
            DisplayName = "Employee",
            DefinitionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Fields =
            [
                new FieldIr
                {
                    FieldId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Slug = "employeeNumber",
                    DisplayName = "Employee Number",
                    DataType = "String",
                    IsRequired = true,
                    IsUnique = true
                }
            ],
            ComponentUses =
            [
                new ComponentUseIr
                {
                    ComponentUseId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    Alias = "person",
                    TargetSlug = "person",
                    Mode = "Embed",
                    PinLatest = true
                }
            ]
        };

        var yaml = MorphYamlSerializer.Serialize(original);
        var loaded = MorphYamlSerializer.Deserialize<DefinitionIr>(yaml);

        loaded.Slug.Should().Be("employee");
        loaded.Kind.Should().Be("Entity");
        loaded.Fields.Should().ContainSingle(f => f.Slug == "employeeNumber" && f.IsUnique);
        loaded.ComponentUses.Should().ContainSingle(c => c.Alias == "person" && c.TargetSlug == "person");
        MorphYamlSerializer.Canonicalize(loaded).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Policy_yaml_round_trips()
    {
        var ir = new PolicyIr
        {
            Name = "employee.viewer",
            Effect = "Allow",
            Actions = ["read", "list"],
            Resources = ["entity:employee"],
            Publish = true
        };

        var yaml = MorphYamlSerializer.Serialize(ir);
        var loaded = MorphYamlSerializer.Deserialize<PolicyIr>(yaml);
        loaded.Name.Should().Be("employee.viewer");
        loaded.Actions.Should().BeEquivalentTo("read", "list");
        loaded.Publish.Should().BeTrue();
    }

    [Fact]
    public void Generate_emits_typescript_and_csharp_stubs()
    {
        var model = new EffectiveModelDto
        {
           Slug = "employee",
            Fingerprint = "abc",
            Fields =
            [
                new EffectiveFieldDto
                {
                    Path = "employeeNumber",
                   Slug = "employeeNumber",
                    DisplayName = "Employee Number",
                    DataType = "String",
                    IsRequired = true
                },
                new EffectiveFieldDto
                {
                    Path = "person.email",
                    Slug = "email",
                    DisplayName = "Email",
                    DataType = "String",
                    IsRequired = false
                }
            ]
        };

        var ts = TypedStubGenerator.GenerateTypeScript(model);
        ts.Should().Contain("export interface Employee");
        ts.Should().Contain("\"person.email\"?: string");

        var cs = TypedStubGenerator.GenerateCSharp(model);
        cs.Should().Contain("public sealed class Employee");
        cs.Should().Contain("PersonEmail");
        cs.Should().Contain("public const string Slug = \"employee\"");
    }

    [Fact]
    public void Init_scaffolds_morph_layout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "morph-cli-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            InitCommand.Run(["--name", "demo"]).Should().Be(0);
            File.Exists(Path.Combine(dir, "morph.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "morph", "definitions", "person.entity.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "morph", "definitions", "employee.entity.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "morph", "policies", "employee.viewer.policy.yaml")).Should().BeTrue();

            var person = MorphYamlSerializer.LoadDefinition(
                Path.Combine(dir, "morph", "definitions", "person.entity.yaml"));
            person.Slug.Should().Be("person");
            person.Fields.Should().NotBeEmpty();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore cleanup races on Windows
            }
        }
    }
}
