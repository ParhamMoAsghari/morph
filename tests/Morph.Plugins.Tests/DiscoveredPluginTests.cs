using FluentAssertions;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Loading;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class DiscoveredPluginTests
{
    [Fact]
    public void Record_exposes_manifest_and_paths()
    {
        var manifest = new PluginManifest
        {
            Id = "morph.test",
            Version = "1.0.0",
            DisplayName = "Test",
            Permissions = ["process.activities"]
        };

        var discovered = new DiscoveredPlugin(manifest, @"C:\plugins\morph.test", @"C:\plugins\morph.test\morph.test.dll");

        discovered.Manifest.Id.Should().Be("morph.test");
        discovered.DirectoryPath.Should().EndWith("morph.test");
        discovered.AssemblyPath.Should().EndWith("morph.test.dll");
    }

    [Fact]
    public void LoadedPlugin_exposes_runtime_handles()
    {
        var pluginDir = Path.Combine(
            PluginDiscoveryTestsHelper.FindHostPluginsRoot(),
            "morph.bale");
        var assemblyPath = Path.Combine(pluginDir, "Morph.Addon.Bale.dll");
        File.Exists(assemblyPath).Should().BeTrue("test requires Morph.Host/plugins/morph.bale");

        var manifest = new PluginManifest
        {
            Id = "morph.test",
            Version = "1.0.0",
            DisplayName = "Test",
            Permissions = []
        };
        var discovered = new DiscoveredPlugin(manifest, pluginDir, assemblyPath);
        var builder = new Morph.Plugins.Hosting.PermissionGatedPluginBuilder(
            manifest.Id,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Morph.Plugins.Process.ProcessActivityRegistry(),
            new Morph.Plugins.Sms.SmsProviderRegistry(),
            new Morph.Plugins.Webhooks.InboundWebhookRegistry(),
            new Morph.Plugins.Webhooks.PluginHttpRouteRegistry(),
            new StubHttpFactory());

        var plugin = new NoOpPlugin(manifest);
        var alc = new PluginLoadContext(discovered.AssemblyPath);
        var loaded = new LoadedPlugin(manifest, discovered.DirectoryPath, discovered.AssemblyPath, alc, plugin, builder);

        loaded.Instance.Should().BeSameAs(plugin);
        loaded.Builder.Should().BeSameAs(builder);
        loaded.Manifest.Id.Should().Be("morph.test");
    }

    private sealed class StubHttpFactory : IPluginHttpClientFactory
    {
        public HttpClient CreateClient(string? name = null) => new();
    }

    private sealed class NoOpPlugin(PluginManifest manifest) : IMorphPlugin
    {
        public PluginManifest Manifest { get; } = manifest;

        public Task RegisterAsync(IPluginBuilder builder, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
