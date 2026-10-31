using Microsoft.Extensions.Logging;
using Morph.Files.Ports;

namespace Morph.Infrastructure.Jobs;

/// <summary>
/// Soft-delete GC stub: deletes object bytes for files marked deleted longer than the retention window.
/// </summary>
public sealed class FileGarbageCollectionJob
{
    public const string JobId = "morph-file-gc";

    private readonly IFileMetadataStore _store;
    private readonly IObjectStorage _storage;
    private readonly ILogger<FileGarbageCollectionJob> _logger;

    public FileGarbageCollectionJob(
        IFileMetadataStore store,
        IObjectStorage storage,
        ILogger<FileGarbageCollectionJob> logger)
    {
        _store = store;
        _storage = storage;
        _logger = logger;
    }

    public Task Execute() => ExecuteCoreAsync(CancellationToken.None);

    private async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var batch = await _store.ListSoftDeletedAsync(cutoff, take: 100, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("File GC stub scanned {Count} soft-deleted files older than {Cutoff}.", batch.Count, cutoff);

        foreach (var file in batch)
        {
            var bucket = await _store.GetBucketAsync(file.BucketId, cancellationToken).ConfigureAwait(false);
            if (bucket is null)
            {
                continue;
            }

            try
            {
                await _storage.DeleteObjectAsync(bucket.PhysicalBucket, file.ObjectKey, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogDebug("GC deleted object {Key}", file.ObjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GC failed for file {FileId}", file.Id);
            }
        }
    }
}
