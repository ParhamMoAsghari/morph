using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Messaging.Outbox;
using Morph.Messaging.Ports;
using Morph.Messaging.Realtime;
using Morph.Messaging.Webhooks;

namespace Morph.Messaging.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers outbox publisher + dispatcher. Caller must register <see cref="IOutboxStore"/>,
    /// <see cref="IWebhookHttpSender"/>, and <see cref="IRealtimeEntityBroadcaster"/>.
    /// </summary>
    public static IServiceCollection AddMorphMessaging(this IServiceCollection services)
    {
        services.TryAddSingleton<IOutboxDispatchService, OutboxDispatchService>();
        services.TryAddScoped<IOutboxPublisher, OutboxPublisher>();
        services.TryAddScoped<WebhookFanoutService>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddOutboxHandler<WebhookDeliveryHandler>();
        services.AddOutboxHandler<RealtimeEntityOutboxHandler>();
        return services;
    }

    public static IServiceCollection AddOutboxHandler<THandler>(this IServiceCollection services)
        where THandler : class, IOutboxHandler
    {
        services.AddScoped<IOutboxHandler, THandler>();
        return services;
    }
}
