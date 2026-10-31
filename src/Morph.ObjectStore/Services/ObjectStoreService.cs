using Morph.Files.Ports;
using Morph.ObjectStore.Ports;

namespace Morph.ObjectStore.Services;

/// <summary>
/// Thin product facade over <see cref="IObjectStorage"/> (MinIO/S3 today).
/// Does not duplicate FileService Access/metadata — use FileService for governed uploads.
/// </summary>
public sealed class ObjectStoreService : IObjectStore
{
    private readonly IObjectStorage _storage;

    public ObjectStoreService(IObjectStorage storage)
    {
        _storage = storage;
    }

    public Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken = default) =>
        _storage.EnsureBucketExistsAsync(bucketName, cancellationToken);

    public Task<Uri> PutAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        _storage.CreatePresignedPutUrlAsync(bucketName, objectKey, contentType, ttl, cancellationToken);

    public Task<Uri> GetAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        _storage.CreatePresignedGetUrlAsync(bucketName, objectKey, ttl, cancellationToken);

    public Task<Uri> GetSignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        GetAsync(bucketName, objectKey, ttl, cancellationToken);

    public Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default) =>
        _storage.DeleteObjectAsync(bucketName, objectKey, cancellationToken);
}
