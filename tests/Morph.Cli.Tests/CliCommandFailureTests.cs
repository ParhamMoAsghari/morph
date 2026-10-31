using FluentAssertions;
using Morph.Cli;
using Morph.Cli.Commands;
using Xunit;

namespace Morph.Cli.Tests;

[Collection("CliWorkingDirectory")]
public sealed class CliCommandFailureTests
{
    [Fact]
    public async Task Login_without_credentials_exits_nonzero()
    {
        await RunInEmptyDir(async () =>
        {
            (await LoginCommand.RunAsync(["--email", "", "--password", ""])).Should().Be(1);
        });
    }

    [Fact]
    public async Task Link_without_required_flags_exits_nonzero()
    {
        await RunInEmptyDir(() =>
        {
            LinkCommand.Run([]).Should().Be(1);
            LinkCommand.Run(["--project", Guid.NewGuid().ToString()]).Should().Be(1);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Push_pull_diff_publish_without_link_exits_nonzero()
    {
        await RunInScaffoldedDir(async () =>
        {
            (await PushCommand.RunAsync([])).Should().Be(1);
            (await PullCommand.RunAsync([])).Should().Be(1);
            (await DiffCommand.RunAsync([])).Should().Be(1);
            (await PublishCommand.RunAsync([])).Should().Be(1);
        });
    }

    [Fact]
    public async Task Access_simulate_missing_principal_exits_nonzero()
    {
        await RunInScaffoldedDir(async () =>
        {
            (await AccessSimulateCommand.RunAsync([])).Should().Be(1);
        });
    }

    [Fact]
    public async Task Access_simulate_with_principal_stub_succeeds_without_host()
    {
        await RunInScaffoldedDir(async () =>
        {
            (await AccessSimulateCommand.RunAsync(["--principal", "user:1", "--action", "read", "--resource", "entity:x"]))
                .Should().Be(0);
        });
    }

    [Fact]
    public async Task Dev_command_prints_stub_and_exits_zero()
    {
        (await Task.FromResult(DevCommand.Run([]))).Should().Be(0);
    }

    [Fact]
    public void Plugin_empty_args_exits_nonzero()
    {
        PluginCommands.Run([]).Should().Be(1);
    }

    [Fact]
    public void Plugin_list_help_exits_zero()
    {
        PluginCommands.Run(["plugin", "list", "--help"]).Should().Be(0);
    }

    [Fact]
    public void Init_without_force_when_exists_exits_nonzero()
    {
        RunInScaffoldedDirSync(dir =>
        {
            InitCommand.Run([]).Should().Be(1);
        });
    }

    private static async Task RunInEmptyDir(Func<Task> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "morph-cli-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            TryDelete(dir);
        }
    }

    private static async Task RunInScaffoldedDir(Func<Task> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "morph-cli-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            InitCommand.Run(["--name", "demo"]).Should().Be(0);
            await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            TryDelete(dir);
        }
    }

    private static void RunInScaffoldedDirSync(Action<string> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "morph-cli-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            InitCommand.Run(["--name", "demo"]).Should().Be(0);
            action(dir);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            TryDelete(dir);
        }
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
