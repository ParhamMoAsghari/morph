namespace Morph.Infrastructure.Persistence.Files;

public sealed class StoredFileRecord
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

    public required string Status { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
