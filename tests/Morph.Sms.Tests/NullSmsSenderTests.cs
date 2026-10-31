using FluentAssertions;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Sms;
using Xunit;

namespace Morph.Sms.Tests;

public sealed class NullSmsSenderTests
{
    [Fact]
    public async Task NullSmsSender_returns_failure_without_provider()
    {
        ISmsSender sut = new NullSmsSender();
        var result = await sut.SendAsync(new SmsMessage("15551234567", "hello"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("No SMS provider");
    }

    [Fact]
    public void SmsProviderRegistry_rejects_duplicate_plugin_registration()
    {
        var registry = new SmsProviderRegistry();
        registry.Register("plugin-a", typeof(FakeSmsProvider));

        var act = () => registry.Register("plugin-a", typeof(FakeSmsProvider));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    private sealed class FakeSmsProvider : ISmsProvider
    {
        public string Name => "plugin-a";

        public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SmsSendResult(true));
    }
}
