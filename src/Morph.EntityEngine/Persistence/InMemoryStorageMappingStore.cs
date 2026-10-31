using System.Collections.Concurrent;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Persistence;

public sealed class InMemoryStorageMappingStore : IStorageMappingStore
{
    private readonly ConcurrentDictionary<Guid, StorageMapping> _byVersion = new();

    public Task<StorageMapping?> GetByVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        _byVersion.TryGetValue(versionId, out var mapping);
        return Task.FromResult(mapping);
    }

    public Task SaveAsync(StorageMapping mapping, CancellationToken cancellationToken = default)
    {
        _byVersion[mapping.VersionId] = mapping;
        return Task.CompletedTask;
    }
}
