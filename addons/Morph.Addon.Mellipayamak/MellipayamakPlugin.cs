using System.Text.Json;
using Morph.Plugins.Abstractions;

namespace Morph.Addon.Mellipayamak;

public sealed class MellipayamakPlugin : IMorphPlugin
{
    public PluginManifest Manifest { get; } = new()
    {
        Id = "morph.mellipayamak",
        Version = "1.0.0",
        DisplayName = "Mellipayamak SMS",
        Author = "Morph",
        MinMorphVersion = "0.1.0",
        Permissions =
        [
            PluginPermissions.SmsSend,
            PluginPermissions.ProcessActivities,
            PluginPermissions.HttpOutbound,
            PluginPermissions.SecretsRead
        ],
        Capabilities = ["sms.provider", "process.activity"]
    };

    public Task RegisterAsync(IPluginBuilder builder, CancellationToken cancellationToken)
    {
        builder.AddSmsProvider<MellipayamakSmsProvider>();
        builder.AddProcessActivity<SendSmsActivity>("plugin:morph.mellipayamak.sendSms");
        builder.AddSecretSchema(JsonDocument.Parse("""
            {
              "type": "object",
              "required": ["username", "password", "from"],
              "properties": {
                "username": { "type": "string" },
                "password": { "type": "string" },
                "from": { "type": "string" },
                "apiBaseUrl": { "type": "string", "format": "uri" }
              }
            }
            """).RootElement.Clone());

        return Task.CompletedTask;
    }
}
