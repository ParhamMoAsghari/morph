namespace Morph.Files.Domain;

public sealed class StoredFile
{
    public Guid Id { get; set; }

    public Guid BucketId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string ObjectKey { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? ChecksumSha256 { get; set; }

    public FileUploadStatus Status { get; set; } = FileUploadStatus.Pending;

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}

public enum FileUploadStatus
{
    Pending = 0,
    Ready = 1,
    Failed = 2
}
