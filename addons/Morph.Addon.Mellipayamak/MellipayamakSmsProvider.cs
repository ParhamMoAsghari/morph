using Morph.Plugins.Abstractions;

namespace Morph.Addon.Mellipayamak;

/// <summary>
/// Stub SMS provider. Real Mellipayamak REST calls land when Morph.Sms secrets injection is wired.
/// </summary>
public sealed class MellipayamakSmsProvider : ISmsProvider
{
    public string Name => "mellipayamak";

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.To) || string.IsNullOrWhiteSpace(message.Body))
        {
            return Task.FromResult(new SmsSendResult(false, Error: "To and Body are required."));
        }

        // Placeholder until project secrets + SSRF client are injected into provider ctor by host.
        return Task.FromResult(new SmsSendResult(
            Succeeded: true,
            ProviderMessageId: $"melli-stub-{Guid.NewGuid():N}"));
    }
}
