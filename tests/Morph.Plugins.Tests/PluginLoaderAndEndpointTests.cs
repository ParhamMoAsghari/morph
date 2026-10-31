using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Hosting;
using Morph.Plugins.Http;
using Morph.Plugins.Loading;
using Morph.Plugins.Process;
using Morph.Plugins.Sms;
using Morph.Plugins.Store;
using Morph.Plugins.Webhooks;
using System.Net;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class PluginLoaderTests
{
    [Fact]
    public async Task LoadAsync_loads_discovered_addon_from_host_plugins_folder()
    {
        var root = PluginDiscoveryTestsHelper.FindHostPluginsRoot();
        var enablement = new InMemoryPluginEnablementStore(defaultEnabled: true);
        var loader = new PluginLoader(
            new PluginDiscovery(NullLogger<PluginDiscovery>.Instance),
            enablement,
            new ProcessActivityRegistry(),
            new SmsProviderRegistry(),
            new InboundWebhookRegistry(),
            new PluginHttpRouteRegistry(),
            new SsrfSafeHttpClientFactory(new StubHttpClientFactory()),
            NullLogger<PluginLoader>.Instance);

        await loader.LoadAsync(root, CancellationToken.None);

        loader.Loaded.Should().NotBeEmpty();
        loader.Loaded.Select(p => p.Manifest.Id).Should().Contain(id => id.StartsWith("morph.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_is_idempotent()
    {
        var root = PluginDiscoveryTestsHelper.FindHostPluginsRoot();
        var loader = CreateLoader();

        await loader.LoadAsync(root, CancellationToken.None);
        var count = loader.Loaded.Count;
        await loader.LoadAsync(root, CancellationToken.None);

        loader.Loaded.Count.Should().Be(count);
    }

    private static PluginLoader CreateLoader() =>
        new(
            new PluginDiscovery(NullLogger<PluginDiscovery>.Instance),
            new InMemoryPluginEnablementStore(defaultEnabled: true),
            new ProcessActivityRegistry(),
            new SmsProviderRegistry(),
            new InboundWebhookRegistry(),
            new PluginHttpRouteRegistry(),
            new SsrfSafeHttpClientFactory(new StubHttpClientFactory()),
            NullLogger<PluginLoader>.Instance);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

internal static class PluginDiscoveryTestsHelper
{
    public static string FindHostPluginsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Morph.Host", "plugins");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate src/Morph.Host/plugins from test output directory.");
    }
}

public sealed class PluginEndpointExtensionsTests
{
    [Fact]
    public async Task MapMorphPlugins_returns_404_for_unknown_route()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(CreateLoader());
                    services.AddSingleton<IInboundWebhookRegistry, InboundWebhookRegistry>();
                    services.AddSingleton<IProcessActivityRegistry, ProcessActivityRegistry>();
                    services.AddSingleton<IPluginHttpRouteRegistry, PluginHttpRouteRegistry>();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapMorphPlugins());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/plugins/v1/missing-plugin/route");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapMorphPlugins_lists_loaded_plugins()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(CreateLoader());
                    services.AddSingleton<IInboundWebhookRegistry, InboundWebhookRegistry>();
                    services.AddSingleton<IProcessActivityRegistry, ProcessActivityRegistry>();
                    services.AddSingleton<IPluginHttpRouteRegistry, PluginHttpRouteRegistry>();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapMorphPlugins());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/plugins/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static PluginLoader CreateLoader() =>
        new(
            new PluginDiscovery(NullLogger<PluginDiscovery>.Instance),
            new InMemoryPluginEnablementStore(defaultEnabled: true),
            new ProcessActivityRegistry(),
            new SmsProviderRegistry(),
            new InboundWebhookRegistry(),
            new PluginHttpRouteRegistry(),
            new SsrfSafeHttpClientFactory(new StubHttpClientFactory()),
            NullLogger<PluginLoader>.Instance);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
