namespace Morph.Plugins.Abstractions;

public interface ISmsProvider
{
    string Name { get; }

    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public sealed record SmsMessage(
    string To,
    string Body,
    string? From = null);

public sealed record SmsSendResult(
    bool Succeeded,
    string? ProviderMessageId = null,
    string? Error = null);

/// <summary>
/// Host SMS facade that selects the enabled project provider (e.g. Mellipayamak).
/// </summary>
public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public interface ISmsProviderRegistry
{
    void Register(string pluginId, Type providerType);

    IReadOnlyCollection<SmsProviderDescriptor> GetAll();

    bool TryGet(string pluginId, out SmsProviderDescriptor descriptor);
}

public sealed record SmsProviderDescriptor(string PluginId, Type ProviderType);
