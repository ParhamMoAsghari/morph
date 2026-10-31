namespace Morph.Runtime.Api.Realtime;

public static class RealtimeChannels
{
    public const string HubPath = "/realtime/v1";

    public static string EntityChannel(Guid environmentId, string entitySlug) =>
        $"env:{environmentId:D}:entity:{entitySlug.Trim().ToLowerInvariant()}";

    public static bool TryParseEntityChannel(
        string channel,
        out Guid environmentId,
        out string entitySlug)
    {
        environmentId = Guid.Empty;
        entitySlug = string.Empty;

        // env:{guid}:entity:{slug}
        const string prefix = "env:";
        const string mid = ":entity:";
        if (!channel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var midIndex = channel.IndexOf(mid, StringComparison.OrdinalIgnoreCase);
        if (midIndex <= prefix.Length)
        {
            return false;
        }

        var envPart = channel[prefix.Length..midIndex];
        var slugPart = channel[(midIndex + mid.Length)..];
        if (!Guid.TryParse(envPart, out environmentId) || string.IsNullOrWhiteSpace(slugPart))
        {
            return false;
        }

        entitySlug = slugPart.Trim().ToLowerInvariant();
        return true;
    }
}
