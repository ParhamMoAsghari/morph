namespace Morph.Infrastructure.Persistence.Files;

public sealed class FileBucketRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required string PhysicalBucket { get; set; }

    public long MaxUploadBytes { get; set; }

    public required string AllowedContentTypesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }
}
