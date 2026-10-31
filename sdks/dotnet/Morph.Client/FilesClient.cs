using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class FilesClient
{
    private readonly MorphHttp _http;

    internal FilesClient(MorphHttp http) => _http = http;

    public Task<FileBucketDto> CreateBucketAsync(
        string slug,
        string? displayName = null,
        long? maxUploadBytes = null,
        IReadOnlyList<string>? allowedContentTypes = null,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<FileBucketDto>(
            HttpMethod.Post,
            "/files/v1/buckets",
            new
            {
                slug,
                displayName,
                maxUploadBytes,
                allowedContentTypes
            },
            environmentId,
            includeEnv: true,
            cancellationToken);

    public Task<UploadSessionDto> CreateUploadAsync(
        Guid bucketId,
        string fileName,
        string contentType,
        long sizeBytes,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<UploadSessionDto>(
            HttpMethod.Post,
            $"/files/v1/buckets/{bucketId:D}/uploads",
            new { fileName, contentType, sizeBytes },
            environmentId,
            includeEnv: true,
            cancellationToken);

    public Task CompleteUploadAsync(
        Guid bucketId,
        Guid fileId,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendAsync(
            HttpMethod.Post,
            $"/files/v1/buckets/{bucketId:D}/uploads/complete",
            new { fileId },
            environmentId,
            includeEnv: true,
            cancellationToken);

    public Task<DownloadUrlDto> GetDownloadUrlAsync(
        Guid fileId,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<DownloadUrlDto>(
            HttpMethod.Get,
            $"/files/v1/{fileId:D}/download",
            environmentId: environmentId,
            includeEnv: true,
            cancellationToken: cancellationToken);

    public Task DeleteAsync(
        Guid fileId,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendAsync(
            HttpMethod.Delete,
            $"/files/v1/{fileId:D}",
            environmentId: environmentId,
            includeEnv: true,
            cancellationToken: cancellationToken);
}
