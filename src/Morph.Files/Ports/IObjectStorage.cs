namespace Morph.Files.Ports;

public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);

    Task<Uri> CreatePresignedPutUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<Uri> CreatePresignedGetUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<ObjectHead?> HeadObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectHead(long ContentLength, string? ContentType, string? ETag);
