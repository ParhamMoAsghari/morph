using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Morph.Plugins.Loading;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class PluginDiscoveryTests
{
    [Fact]
    public void Discover_finds_first_party_plugins_under_host_folder()
    {
        var root = PluginDiscoveryTestsHelper.FindHostPluginsRoot();
        var discovery = new PluginDiscovery(NullLogger<PluginDiscovery>.Instance);

        var plugins = discovery.Discover(root);

        plugins.Should().NotBeEmpty();
        plugins.Select(p => p.Manifest.Id).Should().Contain("morph.bale");
        plugins.Should().OnlyContain(p => File.Exists(p.AssemblyPath));
    }

    [Fact]
    public void Discover_returns_empty_when_root_missing()
    {
        var discovery = new PluginDiscovery(NullLogger<PluginDiscovery>.Instance);
        var plugins = discovery.Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        plugins.Should().BeEmpty();
    }
}
