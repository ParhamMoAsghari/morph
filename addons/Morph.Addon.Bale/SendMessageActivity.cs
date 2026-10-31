using Morph.Plugins.Abstractions;

namespace Morph.Addon.Bale;

public sealed class SendMessageActivity : IProcessActivity
{
    public string TypeName => "plugin:morph.bale.sendMessage";

    public Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken)
    {
        var chatId = context.Variables.TryGetValue("bale.chatId", out var chat) ? chat?.ToString() : null;
        var text = context.Variables.TryGetValue("bale.text", out var body) ? body?.ToString() : null;

        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Variables 'bale.chatId' and 'bale.text' are required.");
        }

        // Stub until bot token secrets + outbound Bale API client are injected.
        context.SetVariable("bale.succeeded", true);
        context.SetVariable("bale.messageId", $"stub-{Guid.NewGuid():N}");
        return Task.CompletedTask;
    }
}
