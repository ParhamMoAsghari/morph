using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;
using Morph.EntityEngine.Storage;

namespace Morph.DataPlane.Cache;

public sealed class MemoryPublishedModelCache : IPublishedModelCache
{
    private readonly ConcurrentDictionary<string, PublishedEntityModel> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceScopeFactory _scopeFactory;

    public MemoryPublishedModelCache(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<PublishedEntityModel?> GetBySlugAsync(
        Guid environmentId,
        string entitySlug,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(environmentId, entitySlug);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IDefinitionStore>();
        var mappings = scope.ServiceProvider.GetRequiredService<IStorageMappingStore>();
        var mapper = scope.ServiceProvider.GetRequiredService<StorageMapper>();
        var schemas = scope.ServiceProvider.GetRequiredService<IEnvironmentSchemaResolver>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var definition = await definitions
            .GetDefinitionBySlugAsync(environmentId, entitySlug, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null
            || definition.Kind != DefinitionKind.Entity
            || definition.CurrentPublishedVersionId is null)
        {
            return null;
        }

        var version = await definitions
            .GetVersionAsync(definition.CurrentPublishedVersionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (version?.EffectiveModel is null)
        {
            return null;
        }

        var envInfo = await schemas.ResolveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (envInfo is null)
        {
            return null;
        }

        var mapping = await mappings
            .GetByVersionAsync(version.VersionId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            mapping = mapper.Map(version.EffectiveModel, envInfo.SchemaName);
            await mappings.SaveAsync(mapping, cancellationToken).ConfigureAwait(false);
        }

        var model = new PublishedEntityModel
        {
            EnvironmentId = environmentId,
            ProjectId = envInfo.ProjectId,
            DefinitionId = definition.DefinitionId,
            Slug = definition.Slug,
            Version = version,
            EffectiveModel = version.EffectiveModel,
            StorageMapping = mapping,
            SchemaName = envInfo.SchemaName,
            LoadedAt = clock.UtcNow
        };

        _cache[key] = model;
        return model;
    }

    public void InvalidateEnvironment(Guid environmentId)
    {
        var prefix = environmentId.ToString("N") + ":";
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            _cache.TryRemove(key, out _);
        }
    }

    public void InvalidateDefinition(Guid environmentId, Guid definitionId)
    {
        foreach (var (key, value) in _cache)
        {
            if (value.EnvironmentId == environmentId && value.DefinitionId == definitionId)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public void InvalidateAll() => _cache.Clear();

    private static string CacheKey(Guid environmentId, string slug) =>
        environmentId.ToString("N") + ":" + slug.Trim().ToLowerInvariant();
}
