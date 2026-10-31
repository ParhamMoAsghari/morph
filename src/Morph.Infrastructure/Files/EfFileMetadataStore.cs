using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Morph.Files.Domain;
using Morph.Files.Ports;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Files;

namespace Morph.Infrastructure.Files;

public sealed class EfFileMetadataStore : IFileMetadataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PlatformDbContext _db;

    public EfFileMetadataStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<FileBucket?> GetBucketAsync(Guid bucketId, CancellationToken cancellationToken = default)
    {
        var row = await _db.FileBuckets.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bucketId && !b.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public async Task<FileBucket?> GetBucketBySlugAsync(
        Guid projectId,
        string slug,
        Guid? environmentId,
        CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var row = await _db.FileBuckets.AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.ProjectId == projectId
                     && b.Slug == normalized
                     && b.EnvironmentId == environmentId
                     && !b.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public async Task<FileBucket> CreateBucketAsync(FileBucket bucket, CancellationToken cancellationToken = default)
    {
        var row = new FileBucketRecord
        {
            Id = bucket.Id,
            ProjectId = bucket.ProjectId,
            EnvironmentId = bucket.EnvironmentId,
            Slug = bucket.Slug,
            DisplayName = bucket.DisplayName,
            PhysicalBucket = bucket.PhysicalBucket,
            MaxUploadBytes = bucket.MaxUploadBytes,
            AllowedContentTypesJson = JsonSerializer.Serialize(bucket.AllowedContentTypes, JsonOptions),
            CreatedAt = bucket.CreatedAt,
            IsDeleted = bucket.IsDeleted
        };

        _db.FileBuckets.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bucket;
    }

    public async Task<StoredFile?> GetFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var row = await _db.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public async Task<StoredFile> CreateFileAsync(StoredFile file, CancellationToken cancellationToken = default)
    {
        var row = ToRecord(file);
        _db.StoredFiles.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return file;
    }

    public async Task UpdateFileAsync(StoredFile file, CancellationToken cancellationToken = default)
    {
        var row = await _db.StoredFiles.FirstOrDefaultAsync(f => f.Id == file.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            throw new InvalidOperationException($"Stored file '{file.Id}' not found.");
        }

        row.SizeBytes = file.SizeBytes;
        row.ContentType = file.ContentType;
        row.ChecksumSha256 = file.ChecksumSha256;
        row.Status = file.Status.ToString();
        row.CompletedAt = file.CompletedAt;
        row.DeletedAt = file.DeletedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StoredFile>> ListSoftDeletedAsync(
        DateTimeOffset olderThan,
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.StoredFiles.AsNoTracking()
            .Where(f => f.DeletedAt != null && f.DeletedAt < olderThan)
            .OrderBy(f => f.DeletedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDomain).ToList();
    }

    private static FileBucket ToDomain(FileBucketRecord row)
    {
        var types = JsonSerializer.Deserialize<List<string>>(row.AllowedContentTypesJson, JsonOptions) ?? ["*/*"];
        return new FileBucket
        {
            Id = row.Id,
            ProjectId = row.ProjectId,
            EnvironmentId = row.EnvironmentId,
            Slug = row.Slug,
            DisplayName = row.DisplayName,
            PhysicalBucket = row.PhysicalBucket,
            MaxUploadBytes = row.MaxUploadBytes,
            AllowedContentTypes = types,
            CreatedAt = row.CreatedAt,
            IsDeleted = row.IsDeleted
        };
    }

    private static StoredFile ToDomain(StoredFileRecord row) => new()
    {
        Id = row.Id,
        BucketId = row.BucketId,
        ProjectId = row.ProjectId,
        EnvironmentId = row.EnvironmentId,
        ObjectKey = row.ObjectKey,
        FileName = row.FileName,
        ContentType = row.ContentType,
        SizeBytes = row.SizeBytes,
        ChecksumSha256 = row.ChecksumSha256,
        Status = Enum.TryParse<FileUploadStatus>(row.Status, true, out var status)
            ? status
            : FileUploadStatus.Pending,
        CreatedBy = row.CreatedBy,
        CreatedAt = row.CreatedAt,
        CompletedAt = row.CompletedAt,
        DeletedAt = row.DeletedAt
    };

    private static StoredFileRecord ToRecord(StoredFile file) => new()
    {
        Id = file.Id,
        BucketId = file.BucketId,
        ProjectId = file.ProjectId,
        EnvironmentId = file.EnvironmentId,
        ObjectKey = file.ObjectKey,
        FileName = file.FileName,
        ContentType = file.ContentType,
        SizeBytes = file.SizeBytes,
        ChecksumSha256 = file.ChecksumSha256,
        Status = file.Status.ToString(),
        CreatedBy = file.CreatedBy,
        CreatedAt = file.CreatedAt,
        CompletedAt = file.CompletedAt,
        DeletedAt = file.DeletedAt
    };
}
