using FluentAssertions;
using Morph.Cli.Commands;
using Xunit;

namespace Morph.Cli.Tests;

public sealed class ArgUtilTests
{
    [Fact]
    public void GetOption_is_case_insensitive()
    {
        ArgUtil.GetOption(["--Email", "a@b.c"], "--email").Should().Be("a@b.c");
        ArgUtil.GetOption(["--email"], "--email").Should().BeNull();
    }

    [Fact]
    public void HasFlag_detects_flags()
    {
        ArgUtil.HasFlag(["push", "--exit-code"], "--exit-code").Should().BeTrue();
        ArgUtil.HasFlag(["push"], "--force").Should().BeFalse();
    }

    [Fact]
    public void GetGuidOption_parses_or_returns_null()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        ArgUtil.GetGuidOption(["--project", id.ToString()], "--project").Should().Be(id);
        ArgUtil.GetGuidOption(["--project", "not-a-guid"], "--project").Should().BeNull();
        ArgUtil.GetGuidOption([], "--project").Should().BeNull();
    }
}
