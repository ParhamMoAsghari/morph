using Morph.Files.Domain;

namespace Morph.Files.Ports;

public interface IFileMetadataStore
{
    Task<FileBucket?> GetBucketAsync(Guid bucketId, CancellationToken cancellationToken = default);

    Task<FileBucket?> GetBucketBySlugAsync(
        Guid projectId,
        string slug,
        Guid? environmentId,
        CancellationToken cancellationToken = default);

    Task<FileBucket> CreateBucketAsync(FileBucket bucket, CancellationToken cancellationToken = default);

    Task<StoredFile?> GetFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<StoredFile> CreateFileAsync(StoredFile file, CancellationToken cancellationToken = default);

    Task UpdateFileAsync(StoredFile file, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredFile>> ListSoftDeletedAsync(
        DateTimeOffset olderThan,
        int take,
        CancellationToken cancellationToken = default);
}
