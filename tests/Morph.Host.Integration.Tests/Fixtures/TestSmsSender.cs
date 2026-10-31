using Morph.Plugins.Abstractions;

namespace Morph.Host.Integration.Tests.Fixtures;

/// <summary>Deterministic SMS provider for host tests (replaces NullSmsSender).</summary>
public sealed class TestSmsSender : ISmsSender
{
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(new SmsSendResult(true, ProviderMessageId: $"test-{Guid.NewGuid():N}"));
}
