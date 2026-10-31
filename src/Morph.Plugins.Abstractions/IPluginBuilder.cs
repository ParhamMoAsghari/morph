using System.Text.Json;

namespace Morph.Plugins.Abstractions;

/// <summary>
/// Host-owned registration surface. Calls are permission-gated against the plugin manifest.
/// </summary>
public interface IPluginBuilder
{
    string PluginId { get; }

    IReadOnlySet<string> GrantedPermissions { get; }

    /// <summary>Requires <see cref="PluginPermissions.ProcessActivities"/>.</summary>
    void AddProcessActivity<TActivity>(string typeName) where TActivity : class, IProcessActivity;

    /// <summary>Requires <see cref="PluginPermissions.SmsSend"/>.</summary>
    void AddSmsProvider<TProvider>() where TProvider : class, ISmsProvider;

    /// <summary>Requires <see cref="PluginPermissions.HttpInbound"/>. Path is under <c>/plugins/v1/{pluginId}/</c>.</summary>
    void AddInboundWebhook(string pathSuffix, IInboundWebhookHandler handler);

    /// <summary>Requires <see cref="PluginPermissions.HttpInbound"/>. Relative to <c>/plugins/v1/{pluginId}/</c>.</summary>
    void AddHttpRoute(string relativePath, Func<HttpContext, CancellationToken, Task> handler);

    /// <summary>Declares project config / secrets schema for CLI and console UI.</summary>
    void AddSecretSchema(JsonElement schema);

    /// <summary>SSRF-safe outbound HTTP. Requires <see cref="PluginPermissions.HttpOutbound"/>.</summary>
    IPluginHttpClientFactory GetHttpClientFactory();

    /// <summary>Access-enforced data client. Requires <see cref="PluginPermissions.DataRead"/> or <see cref="PluginPermissions.DataWrite"/>.</summary>
    IMorphDataClient GetDataClient();
}
