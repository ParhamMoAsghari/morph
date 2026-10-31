using FluentAssertions;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.Access.Persistence;
using Morph.BuildingBlocks.Time;
using Morph.Files.Domain;
using Morph.Files.Ports;
using Morph.Files.Services;
using Morph.Files.Storage;
using NSubstitute;
using Xunit;

namespace Morph.Files.Tests;

public sealed class SignedUrlAuthzTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _envId = Guid.NewGuid();
    private readonly InMemoryRoleStore _roles = new();
    private readonly InMemoryPolicyStore _policies = new();
    private readonly AccessEvaluator _evaluator;
    private readonly InMemoryObjectStorage _storage = new();
    private readonly IFileMetadataStore _store = Substitute.For<IFileMetadataStore>();
    private readonly FileService _files;

    public SignedUrlAuthzTests()
    {
        _roles.EnsureBuiltInRolesAsync(_projectId, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _evaluator = new AccessEvaluator(_roles, _policies, new RoleHierarchyExpander(_roles));
        _files = new FileService(_store, _storage, _evaluator, new SystemClock(), "morph");
    }

    [Fact]
    public async Task Download_denies_presign_when_caller_has_no_role()
    {
        var fileId = Guid.NewGuid();
        var bucketId = Guid.NewGuid();

        _store.GetFileAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(new StoredFile
            {
                Id = fileId,
                BucketId = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                ObjectKey = "k",
                FileName = "photo.png",
                ContentType = "image/png",
                Status = FileUploadStatus.Ready,
                CreatedAt = DateTimeOffset.UtcNow
            });

        _store.GetBucketAsync(bucketId, Arg.Any<CancellationToken>())
            .Returns(new FileBucket
            {
                Id = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                Slug = "photos",
                DisplayName = "Photos",
                PhysicalBucket = "morph",
                CreatedAt = DateTimeOffset.UtcNow
            });

        var result = await _files.CreateDownloadAsync(
            fileId,
            new AccessSubject { PrincipalId = "stranger", Roles = [] });

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("forbidden");
        // Must not mint a URL on deny — Value would throw if success path were taken.
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Download_allows_presign_for_viewer_role()
    {
        await _roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = _projectId,
            PrincipalId = "viewer-1",
            RoleName = BuiltInAppRoles.Viewer,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var fileId = Guid.NewGuid();
        var bucketId = Guid.NewGuid();
        var objectKey = $"{_projectId:N}/{_envId:N}/photos/{fileId:N}";

        _storage.PutObject("morph", objectKey, [1, 2, 3], "image/png");

        _store.GetFileAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(new StoredFile
            {
                Id = fileId,
                BucketId = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                ObjectKey = objectKey,
                FileName = "photo.png",
                ContentType = "image/png",
                Status = FileUploadStatus.Ready,
                CreatedAt = DateTimeOffset.UtcNow
            });

        _store.GetBucketAsync(bucketId, Arg.Any<CancellationToken>())
            .Returns(new FileBucket
            {
                Id = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                Slug = "photos",
                DisplayName = "Photos",
                PhysicalBucket = "morph",
                CreatedAt = DateTimeOffset.UtcNow
            });

        var result = await _files.CreateDownloadAsync(
            fileId,
            new AccessSubject { PrincipalId = "viewer-1", Roles = [] });

        result.IsSuccess.Should().BeTrue();
        result.Value.DownloadUrl.Should().NotBeNull();
        result.Value.DownloadUrl.ToString().Should().Contain("memory://");
    }

    [Fact]
    public async Task Explicit_pbac_deny_blocks_download_even_for_editor()
    {
        await _roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = _projectId,
            PrincipalId = "editor-1",
            RoleName = BuiltInAppRoles.Editor,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _policies.SaveAsync(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "deny-file-download",
            Effect = AccessEffect.Deny,
            Actions = [AccessActions.FileDownload],
            Resources = ["file:photos"],
            Priority = 100,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        var fileId = Guid.NewGuid();
        var bucketId = Guid.NewGuid();

        _store.GetFileAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(new StoredFile
            {
                Id = fileId,
                BucketId = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                ObjectKey = "k",
                FileName = "secret.bin",
                ContentType = "application/octet-stream",
                Status = FileUploadStatus.Ready,
                CreatedAt = DateTimeOffset.UtcNow
            });

        _store.GetBucketAsync(bucketId, Arg.Any<CancellationToken>())
            .Returns(new FileBucket
            {
                Id = bucketId,
                ProjectId = _projectId,
                EnvironmentId = _envId,
                Slug = "photos",
                DisplayName = "Photos",
                PhysicalBucket = "morph",
                CreatedAt = DateTimeOffset.UtcNow
            });

        var result = await _files.CreateDownloadAsync(
            fileId,
            new AccessSubject { PrincipalId = "editor-1" });

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("forbidden");
        result.Error.Message.Should().Contain("deny-file-download");
    }
}
