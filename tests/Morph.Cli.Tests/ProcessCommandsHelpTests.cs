using FluentAssertions;
using Morph.Cli.Commands;
using Xunit;

namespace Morph.Cli.Tests;

public sealed class ProcessCommandsHelpTests
{
    [Fact]
    public async Task Process_help_exits_zero()
    {
        var code = await ProcessCommands.RunAsync(["--help"]);
        code.Should().Be(0);
    }

    [Fact]
    public async Task Process_unknown_command_exits_nonzero()
    {
        var code = await ProcessCommands.RunAsync(["not-a-command"]);
        code.Should().Be(1);
    }
}
