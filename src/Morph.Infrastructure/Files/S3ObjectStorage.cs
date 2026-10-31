using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morph.Files.Ports;
using Morph.Infrastructure.Options;

namespace Morph.Infrastructure.Files;

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3ObjectStorage> _logger;
    private readonly bool _ownsClient;

    public S3ObjectStorage(IOptions<MorphOptions> options, ILogger<S3ObjectStorage> logger)
        : this(CreateClient(options.Value.ObjectStorage), logger, ownsClient: true)
    {
    }

    public S3ObjectStorage(IAmazonS3 client, ILogger<S3ObjectStorage> logger, bool ownsClient = false)
    {
        _client = client;
        _logger = logger;
        _ownsClient = ownsClient;
    }

    public static IAmazonS3 CreateClient(ObjectStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region
        };

        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        return new AmazonS3Client(credentials, config);
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetBucketLocationAsync(bucketName, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                           || string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
        {
            // create below
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogDebug(ex, "Head/location for bucket {Bucket} failed; attempting create.", bucketName);
        }

        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (
            string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "BucketAlreadyExists", StringComparison.OrdinalIgnoreCase)
            || ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // race: another worker created it
        }
    }

    public Task<Uri> CreatePresignedPutUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(ttl),
            ContentType = contentType
        };

        var url = _client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    public Task<Uri> CreatePresignedGetUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl)
        };

        var url = _client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    public async Task DeleteObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(bucketName, objectKey, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ObjectHead?> HeadObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client
                .GetObjectMetadataAsync(bucketName, objectKey, cancellationToken)
                .ConfigureAwait(false);

            return new ObjectHead(response.ContentLength, response.Headers.ContentType, response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
