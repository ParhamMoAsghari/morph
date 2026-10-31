using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Ports;

public interface IStorageMappingStore
{
    Task<StorageMapping?> GetByVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task SaveAsync(StorageMapping mapping, CancellationToken cancellationToken = default);
}
