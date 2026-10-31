using FluentAssertions;
using Morph.Cli;
using Morph.Cli.Commands;
using Xunit;

namespace Morph.Cli.Tests;

public sealed class AdditionalCliCommandTests
{
    [Fact]
    public void Plugin_help_exits_zero()
    {
        var code = PluginCommands.Run(["plugin", "--help"]);
        code.Should().Be(0);
    }

    [Fact]
    public void Plugin_unknown_subcommand_exits_nonzero()
    {
        var code = PluginCommands.Run(["plugin", "not-a-subcommand"]);
        code.Should().Be(1);
    }

    [Fact]
    public async Task Migrate_without_project_link_exits_nonzero()
    {
        var code = await MigrateCommand.RunAsync([]);
        code.Should().Be(1);
    }

    [Fact]
    public async Task Generate_without_project_link_exits_nonzero()
    {
        var code = await GenerateCommand.RunAsync([]);
        code.Should().Be(1);
    }
}
