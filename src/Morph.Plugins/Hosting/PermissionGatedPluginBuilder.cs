using System.Text.Json;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Data;
using Morph.Plugins.Webhooks;

namespace Morph.Plugins.Hosting;

public sealed class PermissionGatedPluginBuilder : IPluginBuilder
{
    private readonly IProcessActivityRegistry _activities;
    private readonly ISmsProviderRegistry _smsProviders;
    private readonly IInboundWebhookRegistry _webhooks;
    private readonly IPluginHttpRouteRegistry _httpRoutes;
    private readonly IPluginHttpClientFactory _httpClientFactory;
    private JsonElement? _secretSchema;

    public PermissionGatedPluginBuilder(
        string pluginId,
        IReadOnlySet<string> grantedPermissions,
        IProcessActivityRegistry activities,
        ISmsProviderRegistry smsProviders,
        IInboundWebhookRegistry webhooks,
        IPluginHttpRouteRegistry httpRoutes,
        IPluginHttpClientFactory httpClientFactory)
    {
        PluginId = pluginId;
        GrantedPermissions = grantedPermissions;
        _activities = activities;
        _smsProviders = smsProviders;
        _webhooks = webhooks;
        _httpRoutes = httpRoutes;
        _httpClientFactory = httpClientFactory;
    }

    public string PluginId { get; }

    public IReadOnlySet<string> GrantedPermissions { get; }

    public JsonElement? SecretSchema => _secretSchema;

    public void AddProcessActivity<TActivity>(string typeName) where TActivity : class, IProcessActivity
    {
        Require(PluginPermissions.ProcessActivities, nameof(AddProcessActivity));
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        _activities.Register(PluginId, typeName, typeof(TActivity));
    }

    public void AddSmsProvider<TProvider>() where TProvider : class, ISmsProvider
    {
        Require(PluginPermissions.SmsSend, nameof(AddSmsProvider));
        _smsProviders.Register(PluginId, typeof(TProvider));
    }

    public void AddInboundWebhook(string pathSuffix, IInboundWebhookHandler handler)
    {
        Require(PluginPermissions.HttpInbound, nameof(AddInboundWebhook));
        _webhooks.Register(PluginId, pathSuffix, handler);
    }

    public void AddHttpRoute(string relativePath, Func<HttpContext, CancellationToken, Task> handler)
    {
        Require(PluginPermissions.HttpInbound, nameof(AddHttpRoute));
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(handler);
        _httpRoutes.Register(PluginId, relativePath, handler);
    }

    public void AddSecretSchema(JsonElement schema)
    {
        Require(PluginPermissions.SecretsRead, nameof(AddSecretSchema));
        _secretSchema = schema;
    }

    public IPluginHttpClientFactory GetHttpClientFactory()
    {
        Require(PluginPermissions.HttpOutbound, nameof(GetHttpClientFactory));
        return _httpClientFactory;
    }

    public IMorphDataClient GetDataClient()
    {
        var canRead = Has(PluginPermissions.DataRead);
        var canWrite = Has(PluginPermissions.DataWrite);
        if (!canRead && !canWrite)
        {
            throw new PluginPermissionException(PluginId, PluginPermissions.DataRead, nameof(GetDataClient));
        }

        return new DeniedMorphDataClient(PluginId, canRead, canWrite);
    }

    private void Require(string permission, string operation)
    {
        if (!Has(permission))
        {
            throw new PluginPermissionException(PluginId, permission, operation);
        }
    }

    private bool Has(string permission) =>
        GrantedPermissions.Contains(permission);
}
