namespace Morph.Files.Domain;

public sealed class FileBucket
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>Physical S3 bucket name (shared Morph prefix + project fragment).</summary>
    public required string PhysicalBucket { get; set; }

    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    public IReadOnlyList<string> AllowedContentTypes { get; set; } = ["*/*"];

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }
}
