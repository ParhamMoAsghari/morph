using System.Text.Json;
using Morph.Plugins.Abstractions;

namespace Morph.Addon.Bale;

public sealed class BalePlugin : IMorphPlugin
{
    public PluginManifest Manifest { get; } = new()
    {
        Id = "morph.bale",
        Version = "1.0.0",
        DisplayName = "Bale Bot",
        Author = "Morph",
        MinMorphVersion = "0.1.0",
        Permissions =
        [
            PluginPermissions.HttpInbound,
            PluginPermissions.HttpOutbound,
            PluginPermissions.ProcessActivities,
            PluginPermissions.SecretsRead
        ],
        Capabilities = ["bot.inbound", "bot.send", "process.activity"]
    };

    public Task RegisterAsync(IPluginBuilder builder, CancellationToken cancellationToken)
    {
        builder.AddInboundWebhook("updates", new BaleWebhookHandler());
        builder.AddProcessActivity<SendMessageActivity>("plugin:morph.bale.sendMessage");
        builder.AddProcessActivity<AnswerCallbackActivity>("plugin:morph.bale.answerCallback");
        builder.AddSecretSchema(JsonDocument.Parse("""
            {
              "type": "object",
              "required": ["botToken"],
              "properties": {
                "botToken": { "type": "string" },
                "webhookSecret": { "type": "string" }
              }
            }
            """).RootElement.Clone());

        return Task.CompletedTask;
    }
}
