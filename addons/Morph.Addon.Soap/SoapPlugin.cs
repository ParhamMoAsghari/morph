using System.Text.Json;
using Morph.Plugins.Abstractions;

namespace Morph.Addon.Soap;

public sealed class SoapPlugin : IMorphPlugin
{
    public PluginManifest Manifest { get; } = new()
    {
        Id = "morph.soap",
        Version = "1.0.0",
        DisplayName = "SOAP Connector",
        Author = "Morph",
        MinMorphVersion = "0.1.0",
        Permissions =
        [
            PluginPermissions.ProcessActivities,
            PluginPermissions.HttpOutbound,
            PluginPermissions.SecretsRead
        ],
        Capabilities = ["process.activity", "soap.invoke"]
    };

    public Task RegisterAsync(IPluginBuilder builder, CancellationToken cancellationToken)
    {
        builder.AddProcessActivity<SoapInvokeActivity>("plugin:morph.soap.invoke");
        builder.AddSecretSchema(JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "defaultEndpoint": { "type": "string", "format": "uri" },
                "defaultSoapAction": { "type": "string" }
              }
            }
            """).RootElement.Clone());

        return Task.CompletedTask;
    }
}
