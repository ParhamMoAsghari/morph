namespace Morph.Plugins.Abstractions;

/// <summary>
/// Capability permissions declared in <c>morph.plugin.json</c> and enforced by the host builder.
/// </summary>
public static class PluginPermissions
{
    public const string ProcessActivities = "process.activities";
    public const string SmsSend = "sms.send";
    public const string HttpOutbound = "http.outbound";
    public const string HttpInbound = "http.inbound";
    public const string DataRead = "data.read";
    public const string DataWrite = "data.write";
    public const string SecretsRead = "secrets.read";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ProcessActivities,
        SmsSend,
        HttpOutbound,
        HttpInbound,
        DataRead,
        DataWrite,
        SecretsRead
    };
}
