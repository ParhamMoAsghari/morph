using System.Collections.Concurrent;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Ports;

namespace Morph.Functions.Runtime.Services;

public sealed class InMemoryFunctionRegistry : IFunctionRegistry
{
    private readonly ConcurrentDictionary<string, FunctionDefinition> _byEnvSlug = new(StringComparer.OrdinalIgnoreCase);

    public Task RegisterAsync(FunctionDefinition definition, CancellationToken cancellationToken = default)
    {
        definition.Slug = definition.Slug.Trim().ToLowerInvariant();
        var key = Key(definition.EnvironmentId, definition.Slug);
        _byEnvSlug.AddOrUpdate(key, definition, (_, _) => definition);
        return Task.CompletedTask;
    }

    public Task<FunctionDefinition?> GetBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        _byEnvSlug.TryGetValue(Key(environmentId, slug.Trim().ToLowerInvariant()), out var def);
        return Task.FromResult(def);
    }

    public Task<IReadOnlyList<FunctionDefinition>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var items = _byEnvSlug.Values
            .Where(d => d.EnvironmentId == environmentId)
            .OrderBy(d => d.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<FunctionDefinition>>(items);
    }

    private static string Key(Guid environmentId, string slug) => $"{environmentId:D}:{slug}";
}
