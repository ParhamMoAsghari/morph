using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.Files.Domain;
using Morph.Files.Ports;

namespace Morph.Files.Services;

public sealed class FileService
{
    public static readonly TimeSpan DefaultUploadTtl = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DefaultDownloadTtl = TimeSpan.FromMinutes(5);

    private readonly IFileMetadataStore _store;
    private readonly IObjectStorage _storage;
    private readonly IAccessEvaluator _access;
    private readonly IClock _clock;
    private readonly string _physicalBucket;

    public FileService(
        IFileMetadataStore store,
        IObjectStorage storage,
        IAccessEvaluator access,
        IClock clock,
        string physicalBucket)
    {
        _store = store;
        _storage = storage;
        _access = access;
        _clock = clock;
        _physicalBucket = physicalBucket;
    }

    public async Task<Result<FileBucket>> CreateBucketAsync(
        Guid projectId,
        Guid? environmentId,
        string slug,
        string displayName,
        AccessSubject subject,
        long? maxUploadBytes = null,
        IReadOnlyList<string>? allowedContentTypes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<FileBucket>(Error.Validation("Bucket slug is required."));
        }

        var decision = await EvaluateAsync(
                projectId,
                environmentId,
                slug,
                AccessActions.FileUpload,
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<FileBucket>(Error.Forbidden(decision.Reason));
        }

        var existing = await _store
            .GetBucketBySlugAsync(projectId, slug, environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure<FileBucket>(Error.Conflict($"Bucket '{slug}' already exists."));
        }

        await _storage.EnsureBucketExistsAsync(_physicalBucket, cancellationToken).ConfigureAwait(false);

        var bucket = new FileBucket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EnvironmentId = environmentId,
            Slug = slug.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? slug : displayName.Trim(),
            PhysicalBucket = _physicalBucket,
            MaxUploadBytes = maxUploadBytes ?? 25 * 1024 * 1024,
            AllowedContentTypes = allowedContentTypes ?? ["*/*"],
            CreatedAt = _clock.UtcNow
        };

        var created = await _store.CreateBucketAsync(bucket, cancellationToken).ConfigureAwait(false);
        return Result.Success(created);
    }

    public async Task<Result<PresignedUpload>> CreateUploadAsync(
        Guid bucketId,
        Guid environmentId,
        string fileName,
        string contentType,
        long? sizeBytes,
        AccessSubject subject,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var bucket = await _store.GetBucketAsync(bucketId, cancellationToken).ConfigureAwait(false);
        if (bucket is null || bucket.IsDeleted)
        {
            return Result.Failure<PresignedUpload>(Error.NotFound("Bucket not found."));
        }

        if (bucket.EnvironmentId is not null && bucket.EnvironmentId != environmentId)
        {
            return Result.Failure<PresignedUpload>(Error.Validation("Bucket is not available in this environment."));
        }

        if (sizeBytes is > 0 && sizeBytes > bucket.MaxUploadBytes)
        {
            return Result.Failure<PresignedUpload>(
                Error.Validation($"File exceeds max upload size of {bucket.MaxUploadBytes} bytes."));
        }

        if (!IsContentTypeAllowed(bucket, contentType))
        {
            return Result.Failure<PresignedUpload>(Error.Validation($"Content type '{contentType}' is not allowed."));
        }

        var decision = await EvaluateAsync(
                bucket.ProjectId,
                environmentId,
                bucket.Slug,
                AccessActions.FileUpload,
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<PresignedUpload>(Error.Forbidden(decision.Reason));
        }

        var fileId = Guid.NewGuid();
        var objectKey = BuildObjectKey(bucket.ProjectId, environmentId, bucket.Slug, fileId);
        var actorId = Guid.TryParse(subject.PrincipalId, out var g) ? g : (Guid?)null;

        var file = new StoredFile
        {
            Id = fileId,
            BucketId = bucket.Id,
            ProjectId = bucket.ProjectId,
            EnvironmentId = environmentId,
            ObjectKey = objectKey,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Status = FileUploadStatus.Pending,
            CreatedBy = actorId,
            CreatedAt = _clock.UtcNow
        };

        await _store.CreateFileAsync(file, cancellationToken).ConfigureAwait(false);

        var uploadTtl = ttl ?? DefaultUploadTtl;
        var url = await _storage
            .CreatePresignedPutUrlAsync(bucket.PhysicalBucket, objectKey, contentType, uploadTtl, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PresignedUpload(fileId, url, _clock.UtcNow.Add(uploadTtl), objectKey));
    }

    public async Task<Result<StoredFile>> CompleteUploadAsync(
        Guid bucketId,
        Guid fileId,
        AccessSubject subject,
        string? checksumSha256 = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _store.GetFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.IsDeleted || file.BucketId != bucketId)
        {
            return Result.Failure<StoredFile>(Error.NotFound("File not found."));
        }

        var bucket = await _store.GetBucketAsync(bucketId, cancellationToken).ConfigureAwait(false);
        if (bucket is null)
        {
            return Result.Failure<StoredFile>(Error.NotFound("Bucket not found."));
        }

        var decision = await EvaluateAsync(
                file.ProjectId,
                file.EnvironmentId,
                bucket.Slug,
                AccessActions.FileUpload,
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<StoredFile>(Error.Forbidden(decision.Reason));
        }

        var head = await _storage
            .HeadObjectAsync(bucket.PhysicalBucket, file.ObjectKey, cancellationToken)
            .ConfigureAwait(false);

        if (head is null)
        {
            file.Status = FileUploadStatus.Failed;
            await _store.UpdateFileAsync(file, cancellationToken).ConfigureAwait(false);
            return Result.Failure<StoredFile>(Error.Validation("Object was not found in storage; upload may be incomplete."));
        }

        file.SizeBytes = head.ContentLength;
        file.ContentType = head.ContentType ?? file.ContentType;
        file.ChecksumSha256 = checksumSha256;
        file.Status = FileUploadStatus.Ready;
        file.CompletedAt = _clock.UtcNow;
        await _store.UpdateFileAsync(file, cancellationToken).ConfigureAwait(false);
        return Result.Success(file);
    }

    public async Task<Result<PresignedDownload>> CreateDownloadAsync(
        Guid fileId,
        AccessSubject subject,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _store.GetFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.IsDeleted)
        {
            return Result.Failure<PresignedDownload>(Error.NotFound("File not found."));
        }

        if (file.Status != FileUploadStatus.Ready)
        {
            return Result.Failure<PresignedDownload>(Error.Validation("File is not ready for download."));
        }

        var bucket = await _store.GetBucketAsync(file.BucketId, cancellationToken).ConfigureAwait(false);
        if (bucket is null || bucket.IsDeleted)
        {
            return Result.Failure<PresignedDownload>(Error.NotFound("Bucket not found."));
        }

        // Access MUST be checked before minting a URL.
        var decision = await EvaluateAsync(
                file.ProjectId,
                file.EnvironmentId,
                bucket.Slug,
                AccessActions.FileDownload,
                subject,
                cancellationToken,
                resourceAttrs: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["file_id"] = file.Id.ToString("N"),
                    ["bucket"] = bucket.Slug,
                    ["created_by"] = file.CreatedBy?.ToString("N"),
                    ["owner_id"] = file.CreatedBy?.ToString("N")
                })
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<PresignedDownload>(Error.Forbidden(decision.Reason));
        }

        var downloadTtl = ttl ?? DefaultDownloadTtl;
        var url = await _storage
            .CreatePresignedGetUrlAsync(bucket.PhysicalBucket, file.ObjectKey, downloadTtl, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PresignedDownload(file.Id, url, _clock.UtcNow.Add(downloadTtl), file.FileName, file.ContentType));
    }

    public async Task<Result> SoftDeleteAsync(
        Guid fileId,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var file = await _store.GetFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.IsDeleted)
        {
            return Result.Failure(Error.NotFound("File not found."));
        }

        var bucket = await _store.GetBucketAsync(file.BucketId, cancellationToken).ConfigureAwait(false);
        if (bucket is null)
        {
            return Result.Failure(Error.NotFound("Bucket not found."));
        }

        var decision = await EvaluateAsync(
                file.ProjectId,
                file.EnvironmentId,
                bucket.Slug,
                AccessActions.Delete,
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure(Error.Forbidden(decision.Reason));
        }

        file.DeletedAt = _clock.UtcNow;
        await _store.UpdateFileAsync(file, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<AccessDecision> EvaluateAsync(
        Guid projectId,
        Guid? environmentId,
        string bucketSlug,
        string action,
        AccessSubject subject,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? resourceAttrs = null)
    {
        var context = new AccessContext
        {
            Subject = subject,
            Action = action,
            Resource = new AccessResource
            {
                Type = $"file:{bucketSlug}",
                ProjectId = projectId,
                EnvironmentId = environmentId,
                Attributes = resourceAttrs ?? new Dictionary<string, object?>()
            }
        };

        return await _access.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildObjectKey(Guid projectId, Guid environmentId, string bucketSlug, Guid fileId) =>
        $"{projectId:N}/{environmentId:N}/{bucketSlug}/{fileId:N}";

    private static bool IsContentTypeAllowed(FileBucket bucket, string contentType)
    {
        if (bucket.AllowedContentTypes.Count == 0
            || bucket.AllowedContentTypes.Any(t => t is "*" or "*/*"))
        {
            return true;
        }

        return bucket.AllowedContentTypes.Any(t =>
            string.Equals(t, contentType, StringComparison.OrdinalIgnoreCase)
            || (t.EndsWith("/*", StringComparison.Ordinal)
                && contentType.StartsWith(t[..^1], StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record PresignedUpload(Guid FileId, Uri UploadUrl, DateTimeOffset ExpiresAt, string ObjectKey);

public sealed record PresignedDownload(
    Guid FileId,
    Uri DownloadUrl,
    DateTimeOffset ExpiresAt,
    string FileName,
    string ContentType);
