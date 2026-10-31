using Morph.Plugins.Abstractions;

namespace Morph.Addon.Mellipayamak;

public sealed class SendSmsActivity : IProcessActivity
{
    private readonly MellipayamakSmsProvider _provider = new();

    public string TypeName => "plugin:morph.mellipayamak.sendSms";

    public async Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken)
    {
        var to = context.Variables.TryGetValue("sms.to", out var toValue) ? toValue?.ToString() : null;
        var body = context.Variables.TryGetValue("sms.body", out var bodyValue) ? bodyValue?.ToString() : null;
        var from = context.Variables.TryGetValue("sms.from", out var fromValue) ? fromValue?.ToString() : null;

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Variables 'sms.to' and 'sms.body' are required.");
        }

        var result = await _provider.SendAsync(new SmsMessage(to, body, from), cancellationToken)
            .ConfigureAwait(false);

        context.SetVariable("sms.succeeded", result.Succeeded);
        context.SetVariable("sms.providerMessageId", result.ProviderMessageId);
        context.SetVariable("sms.error", result.Error);
    }
}
