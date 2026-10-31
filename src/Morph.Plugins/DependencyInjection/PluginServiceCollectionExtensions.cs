using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Hosting;
using Morph.Plugins.Http;
using Morph.Plugins.Loading;
using Morph.Plugins.Options;
using Morph.Plugins.Process;
using Morph.Plugins.Sms;
using Morph.Plugins.Store;
using Morph.Plugins.Webhooks;

namespace Morph.Plugins.DependencyInjection;

public static class PluginServiceCollectionExtensions
{
    public static IServiceCollection AddMorphPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PluginHostOptions>(options =>
        {
            configuration.GetSection(PluginHostOptions.SectionName).Bind(options);

            var envPath = configuration["Plugins:Path"]
                ?? configuration["MORPH_Plugins__Path"];
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                options.Path = envPath;
            }
        });

        var defaultEnabled = configuration.GetValue("Plugins:EnableAllDiscovered", true);

        services.TryAddSingleton<IPluginEnablementStore>(_ => new InMemoryPluginEnablementStore(defaultEnabled));
        // Replace ProcessEngine empty stubs so plugin-registered activities + SSRF HTTP are live.
        services.RemoveAll<IProcessActivityRegistry>();
        services.AddSingleton<IProcessActivityRegistry, ProcessActivityRegistry>();
        services.TryAddSingleton<ISmsProviderRegistry, SmsProviderRegistry>();
        services.TryAddSingleton<IInboundWebhookRegistry, InboundWebhookRegistry>();
        services.TryAddSingleton<IPluginHttpRouteRegistry, PluginHttpRouteRegistry>();
        services.TryAddSingleton<ISmsSender, NullSmsSender>();

        services.AddHttpClient(SsrfSafeHttpClientFactory.ClientName)
            .AddHttpMessageHandler(() => new SsrfGuardHandler());

        services.RemoveAll<IPluginHttpClientFactory>();
        services.AddSingleton<IPluginHttpClientFactory, SsrfSafeHttpClientFactory>();
        services.TryAddSingleton<PluginDiscovery>();
        services.TryAddSingleton<PluginLoader>();
        services.AddHostedService<PluginHostedService>();

        return services;
    }
}
