using FluentAssertions;
using Morph.Cli.Config;
using Morph.Cli.Schema;
using Xunit;

namespace Morph.Cli.Tests;

[Collection("CliWorkingDirectory")]
public sealed class ProjectConfigStoreTests
{
    [Fact]
    public void Load_returns_defaults_when_morph_yaml_missing()
    {
        using var dir = TempProject.CreateEmpty();
        var config = ProjectConfigStore.Load(dir.Root);
        config.Name.Should().Be("morph-app");
        config.ApiUrl.Should().Be("http://localhost:5100");
        config.ProjectId.Should().BeNull();
    }

    [Fact]
    public void Save_and_load_round_trips_project_config()
    {
        using var dir = TempProject.CreateEmpty();
        var original = new ProjectConfig
        {
            Name = "demo",
            Version = "1.2.3",
            ApiUrl = "http://api.test",
            ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EnvironmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            OrganizationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Codegen = new CodegenConfig { Typescript = "ts/out", Csharp = "Cs/Gen" },
            Plugins = [new PluginRefConfig { Id = "sample", Version = "1.0.0", ConfigRef = "cfg" }]
        };

        ProjectConfigStore.Save(dir.Root, original);
        File.Exists(System.IO.Path.Combine(dir.Root, ProjectPaths.MorphYaml)).Should().BeTrue();

        var loaded = ProjectConfigStore.Load(dir.Root);
        loaded.Name.Should().Be("demo");
        loaded.ApiUrl.Should().Be("http://api.test");
        loaded.ProjectId.Should().Be(original.ProjectId);
        loaded.Codegen.Typescript.Should().Be("ts/out");
        loaded.Plugins.Should().ContainSingle(p => p.Id == "sample");
    }

    [Fact]
    public void Link_state_round_trips()
    {
        using var dir = TempProject.CreateEmpty();
        ProjectConfigStore.LoadLink(dir.Root).Should().BeNull();

        var link = new LinkState
        {
            ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EnvironmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            OrganizationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ApiUrl = "http://linked",
            LinkedAt = DateTimeOffset.Parse("2026-01-15T10:00:00Z")
        };

        ProjectConfigStore.SaveLink(dir.Root, link);
        var loaded = ProjectConfigStore.LoadLink(dir.Root);
        loaded.Should().NotBeNull();
        loaded!.ProjectId.Should().Be(link.ProjectId);
        loaded.ApiUrl.Should().Be("http://linked");
        loaded.LinkedAt.Should().Be(link.LinkedAt);
    }

    [Fact]
    public void Credentials_round_trip()
    {
        using var dir = TempProject.CreateEmpty();
        ProjectConfigStore.LoadCredentials(dir.Root).Should().BeNull();

        var store = new CredentialStore
        {
            AccessToken = "at",
            RefreshToken = "rt",
            Email = "a@b.c",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        ProjectConfigStore.SaveCredentials(dir.Root, store);
        var loaded = ProjectConfigStore.LoadCredentials(dir.Root);
        loaded!.AccessToken.Should().Be("at");
        loaded.Email.Should().Be("a@b.c");
        loaded.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void Enumerate_definition_and_policy_files()
    {
        using var dir = TempProject.CreateScaffolded();
        ProjectConfigStore.EnumerateDefinitionFiles(dir.Root).Should().NotBeEmpty();
        ProjectConfigStore.EnumeratePolicyFiles(dir.Root).Should().NotBeEmpty();
        ProjectConfigStore.LoadAllDefinitions(dir.Root).Should().Contain(d => d.Slug == "person");
    }

    [Fact]
    public void FindRoot_walks_up_to_morph_yaml()
    {
        using var dir = TempProject.CreateScaffolded();
        var nested = System.IO.Path.Combine(dir.Root, "a", "b");
        Directory.CreateDirectory(nested);

        ProjectPaths.FindRoot(nested).Should().Be(dir.Root);
    }

    private sealed class TempProject : IDisposable
    {
        public string Root { get; }

        private TempProject(string root) => Root = root;

        public static TempProject CreateEmpty()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morph-cli-cfg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempProject(root);
        }

        public static TempProject CreateScaffolded()
        {
            var root = CreateEmpty().Root;
            var cwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(root);
                Morph.Cli.Commands.InitCommand.Run(["--name", "demo"]).Should().Be(0);
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
            }

            return new TempProject(root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // ignore cleanup races on Windows
            }
        }
    }
}
