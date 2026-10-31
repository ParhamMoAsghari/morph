using FluentAssertions;
using Morph.Messaging.Webhooks;
using Xunit;

namespace Morph.Messaging.Tests;

public sealed class WebhookSignatureTests
{
    [Fact]
    public void Sign_and_validate_round_trip()
    {
        var secret = "test-webhook-secret";
        var body = """{"type":"record.created","id":"abc"}""";
        var now = DateTimeOffset.Parse("2026-10-29T12:00:00Z");

        var header = WebhookSignature.Sign(secret, body, now);

        header.Should().StartWith("t=");
        header.Should().Contain(",v1=");

        var ok = WebhookSignature.TryValidate(
            secret,
            body,
            header,
            TimeSpan.FromMinutes(5),
            now,
            out var reason);

        ok.Should().BeTrue(reason);
        reason.Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_tampered_body()
    {
        var secret = "test-webhook-secret";
        var now = DateTimeOffset.UtcNow;
        var header = WebhookSignature.Sign(secret, """{"a":1}""", now);

        var ok = WebhookSignature.TryValidate(
            secret,
            """{"a":2}""",
            header,
            TimeSpan.FromMinutes(5),
            now,
            out var reason);

        ok.Should().BeFalse();
        reason.Should().Contain("mismatch");
    }

    [Fact]
    public void Validate_rejects_stale_timestamp()
    {
        var secret = "test-webhook-secret";
        var stamped = DateTimeOffset.UtcNow.AddMinutes(-30);
        var header = WebhookSignature.Sign(secret, "hello", stamped);

        var ok = WebhookSignature.TryValidate(
            secret,
            "hello",
            header,
            TimeSpan.FromMinutes(5),
            DateTimeOffset.UtcNow,
            out var reason);

        ok.Should().BeFalse();
        reason.Should().Contain("skew");
    }

    [Fact]
    public void Header_name_is_X_Morph_Signature()
    {
        WebhookSignature.HeaderName.Should().Be("X-Morph-Signature");
    }
}
