namespace Morph.ObjectStore.Ports;

/// <summary>
/// Morph-native object store facade over Files / S3 backends (Put / Get / GetSignedUrl).
/// </summary>
public interface IObjectStore
{
    Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>Mints a presigned PUT URL for uploading an object.</summary>
    Task<Uri> PutAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    /// <summary>Mints a presigned GET URL for downloading an object.</summary>
    Task<Uri> GetAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    /// <summary>Alias for <see cref="GetAsync"/>.</summary>
    Task<Uri> GetSignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);
}
