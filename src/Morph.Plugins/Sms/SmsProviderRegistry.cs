using System.Collections.Concurrent;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Sms;

public sealed class SmsProviderRegistry : ISmsProviderRegistry
{
    private readonly ConcurrentDictionary<string, SmsProviderDescriptor> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, Type providerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(providerType);

        if (!typeof(ISmsProvider).IsAssignableFrom(providerType))
        {
            throw new ArgumentException(
                $"Type '{providerType.FullName}' does not implement {nameof(ISmsProvider)}.",
                nameof(providerType));
        }

        var descriptor = new SmsProviderDescriptor(pluginId, providerType);
        if (!_providers.TryAdd(pluginId, descriptor))
        {
            throw new InvalidOperationException(
                $"SMS provider for plugin '{pluginId}' is already registered.");
        }
    }

    public IReadOnlyCollection<SmsProviderDescriptor> GetAll() => _providers.Values.ToArray();

    public bool TryGet(string pluginId, out SmsProviderDescriptor descriptor) =>
        _providers.TryGetValue(pluginId, out descriptor!);
}

public sealed class NullSmsSender : ISmsSender
{
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(new SmsSendResult(false, Error: "No SMS provider is configured."));
}
