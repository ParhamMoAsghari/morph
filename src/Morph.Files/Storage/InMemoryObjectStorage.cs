using System.Collections.Concurrent;
using Morph.Files.Ports;

namespace Morph.Files.Storage;

/// <summary>
/// Process-local object store for tests and hosts without MinIO.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemoryObject>> _buckets = new(StringComparer.Ordinal);

    public Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        _buckets.GetOrAdd(bucketName, _ => new ConcurrentDictionary<string, MemoryObject>(StringComparer.Ordinal));
        return Task.CompletedTask;
    }

    public Task<Uri> CreatePresignedPutUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        EnsureBucketExistsAsync(bucketName, cancellationToken).GetAwaiter().GetResult();
        var url = new Uri($"memory://{bucketName}/{Uri.EscapeDataString(objectKey)}?verb=PUT&ttl={(int)ttl.TotalSeconds}&ct={Uri.EscapeDataString(contentType)}");
        return Task.FromResult(url);
    }

    public Task<Uri> CreatePresignedGetUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var url = new Uri($"memory://{bucketName}/{Uri.EscapeDataString(objectKey)}?verb=GET&ttl={(int)ttl.TotalSeconds}");
        return Task.FromResult(url);
    }

    public Task DeleteObjectAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        if (_buckets.TryGetValue(bucketName, out var objects))
        {
            objects.TryRemove(objectKey, out _);
        }

        return Task.CompletedTask;
    }

    public Task<ObjectHead?> HeadObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (_buckets.TryGetValue(bucketName, out var objects)
            && objects.TryGetValue(objectKey, out var obj))
        {
            return Task.FromResult<ObjectHead?>(new ObjectHead(obj.Bytes.Length, obj.ContentType, obj.ETag));
        }

        return Task.FromResult<ObjectHead?>(null);
    }

    public void PutObject(string bucketName, string objectKey, byte[] bytes, string contentType)
    {
        var bucket = _buckets.GetOrAdd(bucketName, _ => new ConcurrentDictionary<string, MemoryObject>(StringComparer.Ordinal));
        bucket[objectKey] = new MemoryObject(bytes, contentType, $"\"{Guid.NewGuid():N}\"");
    }

    private sealed record MemoryObject(byte[] Bytes, string ContentType, string ETag);
}
