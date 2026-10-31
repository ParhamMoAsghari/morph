using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Webhooks;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Webhooks;

public sealed class EfWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PlatformDbContext _db;

    public EfWebhookSubscriptionStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<WebhookSubscription> CreateAsync(
        WebhookSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var row = ToRecord(subscription);
        _db.WebhookSubscriptions.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(row);
    }

    public async Task<IReadOnlyList<WebhookSubscription>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<WebhookSubscription>> ListMatchingAsync(
        Guid projectId,
        Guid environmentId,
        string entitySlug,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(w => w.ProjectId == projectId && w.IsEnabled)
            .Where(w => w.EnvironmentId == null || w.EnvironmentId == environmentId)
            .Where(w => w.EntitySlug == null || w.EntitySlug == "" || w.EntitySlug == entitySlug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(ToDomain)
            .Where(s => s.EventTypes.Count == 0
                        || s.EventTypes.Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase)
                                                 || e == "*"))
            .ToList();
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == subscriptionId && w.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        _db.WebhookSubscriptions.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _db.WebhookSubscriptions.CountAsync(w => w.ProjectId == projectId, cancellationToken);

    private static WebhookSubscriptionRecord ToRecord(WebhookSubscription s) => new()
    {
        Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
        ProjectId = s.ProjectId,
        EnvironmentId = s.EnvironmentId,
        TargetUrl = s.TargetUrl,
        SigningSecret = s.SigningSecret,
        EntitySlug = s.EntitySlug,
        EventTypesJson = JsonSerializer.Serialize(s.EventTypes ?? [], JsonOptions),
        IsEnabled = s.IsEnabled,
        CreatedAt = s.CreatedAt
    };

    private static WebhookSubscription ToDomain(WebhookSubscriptionRecord row)
    {
        IReadOnlyList<string> events = [];
        try
        {
            events = JsonSerializer.Deserialize<List<string>>(row.EventTypesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            events = [];
        }

        return new WebhookSubscription
        {
            Id = row.Id,
            ProjectId = row.ProjectId,
            EnvironmentId = row.EnvironmentId,
            TargetUrl = row.TargetUrl,
            SigningSecret = row.SigningSecret,
            EntitySlug = row.EntitySlug,
            EventTypes = events,
            IsEnabled = row.IsEnabled,
            CreatedAt = row.CreatedAt
        };
    }
}
