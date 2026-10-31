namespace Morph.Plugins.Abstractions;

public interface IInboundWebhookHandler
{
    Task HandleAsync(HttpContext context, CancellationToken cancellationToken);
}

public interface IInboundWebhookRegistry
{
    void Register(string pluginId, string pathSuffix, IInboundWebhookHandler handler);

    IReadOnlyCollection<InboundWebhookRegistration> GetAll();

    bool TryResolve(string pluginId, string pathSuffix, out InboundWebhookRegistration registration);
}

public sealed record InboundWebhookRegistration(
    string PluginId,
    string PathSuffix,
    IInboundWebhookHandler Handler);
