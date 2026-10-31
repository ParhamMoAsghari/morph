using FluentAssertions;
using Morph.Messaging.Webhooks;
using Xunit;

namespace Morph.Messaging.Tests;

public sealed class WebhookSignatureEdgeCaseTests
{
    [Fact]
    public void Validate_accepts_empty_body_when_signature_matches()
    {
        var secret = "test-webhook-secret";
        var now = DateTimeOffset.Parse("2026-10-29T12:00:00Z");
        var header = WebhookSignature.Sign(secret, "", now);

        WebhookSignature.TryValidate(secret, "", header, TimeSpan.FromMinutes(5), now, out var reason)
            .Should().BeTrue(reason);
    }

    [Fact]
    public void Validate_rejects_wrong_secret()
    {
        var now = DateTimeOffset.UtcNow;
        var header = WebhookSignature.Sign("correct-secret", "payload", now);

        var ok = WebhookSignature.TryValidate(
            "wrong-secret",
            "payload",
            header,
            TimeSpan.FromMinutes(5),
            now,
            out var reason);

        ok.Should().BeFalse();
        reason.Should().Contain("mismatch");
    }

    [Fact]
    public void Validate_rejects_empty_secret()
    {
        var now = DateTimeOffset.UtcNow;
        var header = WebhookSignature.Sign("secret", "payload", now);

        WebhookSignature.TryValidate("", "payload", header, TimeSpan.FromMinutes(5), now, out var reason)
            .Should().BeFalse();
        reason.Should().Contain("Secret");
    }
}
