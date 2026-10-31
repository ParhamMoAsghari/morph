using FluentAssertions;
using Morph.Cli;
using Xunit;

namespace Morph.Cli.Tests;

public sealed class PluginCommandsExtendedTests
{
    [Fact]
    public void Plugin_list_with_missing_directory_exits_zero()
    {
        var missing = Path.Combine(Path.GetTempPath(), "morph-no-plugins-" + Guid.NewGuid().ToString("N"));
        PluginCommands.Run(["plugin", "list", "--path", missing]).Should().Be(0);
    }

    [Fact]
    public void Plugin_list_reads_manifest_from_custom_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "morph-plugins-" + Guid.NewGuid().ToString("N"));
        var pluginDir = Path.Combine(root, "demo.addon");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(
            Path.Combine(pluginDir, "morph.plugin.json"),
            """
            {
              "id": "demo.addon",
              "version": "1.0.0",
              "displayName": "Demo Addon",
              "permissions": []
            }
            """);

        try
        {
            PluginCommands.Run(["plugin", "list", "--path", root]).Should().Be(0);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Plugin_install_without_source_exits_nonzero()
    {
        PluginCommands.Run(["plugin", "install"]).Should().Be(1);
    }

    [Fact]
    public void Plugin_install_missing_source_exits_nonzero()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));
        PluginCommands.Run(["plugin", "install", missing]).Should().Be(1);
    }

    [Fact]
    public void Plugin_install_invalid_file_type_exits_nonzero()
    {
        var root = Path.Combine(Path.GetTempPath(), "morph-bad-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "readme.txt");
        File.WriteAllText(file, "nope");
        try
        {
            PluginCommands.Run(["plugin", "install", file]).Should().Be(1);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Plugin_install_copies_addon_folder()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "morph-src-" + Guid.NewGuid().ToString("N"));
        var targetRoot = Path.Combine(Path.GetTempPath(), "morph-target-" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(sourceRoot, "demo.addon");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(
            Path.Combine(sourceDir, "morph.plugin.json"),
            """
            {
              "id": "demo.addon",
              "version": "2.0.0",
              "displayName": "Demo Addon",
              "permissions": []
            }
            """);
        File.WriteAllText(Path.Combine(sourceDir, "demo.addon.dll"), "fake");

        try
        {
            PluginCommands.Run(["plugin", "install", sourceDir, "--path", targetRoot]).Should().Be(0);
            Directory.Exists(Path.Combine(targetRoot, "demo.addon")).Should().BeTrue();
            File.Exists(Path.Combine(targetRoot, "demo.addon", ".morph-install.json")).Should().BeTrue();
        }
        finally
        {
            TryDelete(sourceRoot);
            TryDelete(targetRoot);
        }
    }

    [Fact]
    public void Plugin_wrong_top_level_command_exits_nonzero()
    {
        PluginCommands.Run(["not-plugin", "list"]).Should().Be(1);
    }

    private static void TryDelete(string dir)
    {
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
