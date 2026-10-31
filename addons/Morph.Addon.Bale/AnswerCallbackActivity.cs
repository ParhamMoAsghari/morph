using Morph.Plugins.Abstractions;

namespace Morph.Addon.Bale;

public sealed class AnswerCallbackActivity : IProcessActivity
{
    public string TypeName => "plugin:morph.bale.answerCallback";

    public Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken)
    {
        var callbackId = context.Variables.TryGetValue("bale.callbackQueryId", out var id)
            ? id?.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(callbackId))
        {
            throw new InvalidOperationException("Variable 'bale.callbackQueryId' is required.");
        }

        context.SetVariable("bale.callbackAnswered", true);
        return Task.CompletedTask;
    }
}
