using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Morph.Messaging.Webhooks;

/// <summary>
/// HMAC-SHA256 webhook signing. Header value format: <c>t={unixSeconds},v1={hex}</c>.
/// </summary>
public static class WebhookSignature
{
    public const string HeaderName = "X-Morph-Signature";

    public static string Sign(string secret, ReadOnlySpan<byte> body, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var unix = timestamp.ToUnixTimeSeconds();
        var payload = BuildSigningPayload(unix, body);
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, payload);
        return $"t={unix},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static string Sign(string secret, string bodyUtf8, DateTimeOffset timestamp) =>
        Sign(secret, Encoding.UTF8.GetBytes(bodyUtf8), timestamp);

    public static bool TryValidate(
        string secret,
        ReadOnlySpan<byte> body,
        string? headerValue,
        TimeSpan maxSkew,
        DateTimeOffset now,
        out string? failureReason)
    {
        failureReason = null;
        if (string.IsNullOrWhiteSpace(secret))
        {
            failureReason = "Secret is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            failureReason = "Missing signature header.";
            return false;
        }

        if (!TryParseHeader(headerValue, out var unix, out var providedHex))
        {
            failureReason = "Malformed signature header.";
            return false;
        }

        var stamped = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (Math.Abs((now - stamped).TotalSeconds) > maxSkew.TotalSeconds)
        {
            failureReason = "Signature timestamp outside allowed skew.";
            return false;
        }

        var expected = Sign(secret, body, stamped);
        if (!TryParseHeader(expected, out _, out var expectedHex))
        {
            failureReason = "Failed to compute expected signature.";
            return false;
        }

        var providedBytes = Convert.FromHexString(providedHex);
        var expectedBytes = Convert.FromHexString(expectedHex);
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            failureReason = "Signature mismatch.";
            return false;
        }

        return true;
    }

    public static bool TryValidate(
        string secret,
        string bodyUtf8,
        string? headerValue,
        TimeSpan maxSkew,
        DateTimeOffset now,
        out string? failureReason) =>
        TryValidate(secret, Encoding.UTF8.GetBytes(bodyUtf8), headerValue, maxSkew, now, out failureReason);

    private static byte[] BuildSigningPayload(long unixSeconds, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes(unixSeconds.ToString(CultureInfo.InvariantCulture) + ".");
        var buffer = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, buffer, 0, prefix.Length);
        body.CopyTo(buffer.AsSpan(prefix.Length));
        return buffer;
    }

    private static bool TryParseHeader(string header, out long unix, out string hex)
    {
        unix = 0;
        hex = string.Empty;
        long? t = null;
        string? v1 = null;

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq];
            var value = part[(eq + 1)..];
            if (key.Equals("t", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                t = parsed;
            }
            else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
            {
                v1 = value;
            }
        }

        if (t is null || string.IsNullOrWhiteSpace(v1) || v1.Length % 2 != 0)
        {
            return false;
        }

        unix = t.Value;
        hex = v1;
        return true;
    }
}
