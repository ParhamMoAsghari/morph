using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Morph.Files.Ports;
using Morph.Files.Storage;
using Morph.Infrastructure.Files;
using Morph.Infrastructure.Options;
using Xunit;

namespace Morph.Files.Tests;

/// <summary>
/// Contract tests against <see cref="IObjectStorage"/>. Uses in-memory by default;
/// optionally MinIO via Testcontainers when Docker Hub is available (set MORPH_RUN_MINIO_TESTS=1).
/// </summary>
public sealed class ObjectStorageContractTests
{
    [Fact]
    public async Task InMemory_presign_put_get_and_head_roundtrip()
    {
        var storage = new InMemoryObjectStorage();
        await RunContractAsync(storage, "morph-contract");
    }

    [Fact]
    public async Task Minio_presign_contract_when_enabled()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("MORPH_RUN_MINIO_TESTS"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            // Skip without failing CI when Docker Hub / MinIO image is unavailable.
            return;
        }

        await using var fixture = await MinioFixture.TryStartAsync();
        if (fixture is null)
        {
            return;
        }

        var options = new ObjectStorageOptions
        {
            Endpoint = fixture.Endpoint,
            AccessKey = fixture.AccessKey,
            SecretKey = fixture.SecretKey,
            ForcePathStyle = true,
            Region = "us-east-1",
            BucketPrefix = "morph"
        };

        using var client = S3ObjectStorage.CreateClient(options);
        using var storage = new S3ObjectStorage(client, NullLogger<S3ObjectStorage>.Instance);
        await RunContractAsync(storage, "morph-contract");
    }

    private static async Task RunContractAsync(IObjectStorage storage, string bucket)
    {
        await storage.EnsureBucketExistsAsync(bucket);

        var key = $"proj/env/bucket/{Guid.NewGuid():N}";
        var put = await storage.CreatePresignedPutUrlAsync(bucket, key, "text/plain", TimeSpan.FromMinutes(5));
        put.Should().NotBeNull();
        put.IsAbsoluteUri.Should().BeTrue();

        if (storage is InMemoryObjectStorage memory)
        {
            memory.PutObject(bucket, key, "hello"u8.ToArray(), "text/plain");
        }
        else
        {
            using var http = new HttpClient();
            using var content = new ByteArrayContent("hello"u8.ToArray());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            var response = await http.PutAsync(put, content);
            response.IsSuccessStatusCode.Should().BeTrue($"PUT failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var head = await storage.HeadObjectAsync(bucket, key);
        head.Should().NotBeNull();
        head!.ContentLength.Should().Be(5);

        var get = await storage.CreatePresignedGetUrlAsync(bucket, key, TimeSpan.FromMinutes(5));
        get.Should().NotBeNull();

        await storage.DeleteObjectAsync(bucket, key);
        var missing = await storage.HeadObjectAsync(bucket, key);
        missing.Should().BeNull();
    }
}

internal sealed class MinioFixture : IAsyncDisposable
{
    private readonly Testcontainers.Minio.MinioContainer _container;

    private MinioFixture(Testcontainers.Minio.MinioContainer container, string endpoint, string accessKey, string secretKey)
    {
        _container = container;
        Endpoint = endpoint;
        AccessKey = accessKey;
        SecretKey = secretKey;
    }

    public string Endpoint { get; }

    public string AccessKey { get; }

    public string SecretKey { get; }

    public static async Task<MinioFixture?> TryStartAsync()
    {
        try
        {
            const string user = "morphminio";
            const string pass = "morphminio";
            var container = new Testcontainers.Minio.MinioBuilder()
                .WithUsername(user)
                .WithPassword(pass)
                .Build();

            await container.StartAsync();
            var endpoint = container.GetConnectionString();
            return new MinioFixture(container, endpoint, user, pass);
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
