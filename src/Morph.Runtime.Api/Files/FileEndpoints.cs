using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Morph.Access.Domain;
using Morph.DataPlane.Cache;
using Morph.Files.Services;

namespace Morph.Runtime.Api.Files;

public static class FileEndpoints
{
    public const string EnvironmentHeader = "X-Morph-Env-Id";

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/files/v1")
            .WithTags("Files")
            .RequireAuthorization();

        group.MapGet("/_meta/status", () => Results.Ok(new
        {
            status = "ready",
            phase = 4,
            message = "Buckets + Access-gated presigned PUT/GET."
        }));

        group.MapPost("/buckets", CreateBucketAsync);
        group.MapPost("/buckets/{bucketId:guid}/uploads", CreateUploadAsync);
        group.MapPost("/buckets/{bucketId:guid}/uploads/complete", CompleteUploadAsync);
        group.MapGet("/{fileId:guid}/download", DownloadAsync);
        group.MapDelete("/{fileId:guid}", DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateBucketAsync(
        CreateBucketRequest request,
        HttpContext http,
        FileService files,
        IEnvironmentSchemaResolver envs,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var env = await envs.ResolveAsync(envId, cancellationToken).ConfigureAwait(false);
        if (env is null)
        {
            return Results.NotFound(new { detail = "Environment not found." });
        }

        var result = await files.CreateBucketAsync(
                env.ProjectId,
                envId,
                request.Slug,
                request.DisplayName ?? request.Slug,
                BuildSubject(http.User),
                request.MaxUploadBytes,
                request.AllowedContentTypes,
                cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, b => Results.Created($"/files/v1/buckets/{b.Id}", new
        {
            id = b.Id,
            projectId = b.ProjectId,
            environmentId = b.EnvironmentId,
            slug = b.Slug,
            displayName = b.DisplayName,
            maxUploadBytes = b.MaxUploadBytes,
            createdAt = b.CreatedAt
        }));
    }

    private static async Task<IResult> CreateUploadAsync(
        Guid bucketId,
        CreateUploadRequest request,
        HttpContext http,
        FileService files,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var result = await files.CreateUploadAsync(
                bucketId,
                envId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                BuildSubject(http.User),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, u => Results.Ok(new
        {
            fileId = u.FileId,
            uploadUrl = u.UploadUrl.ToString(),
            expiresAt = u.ExpiresAt,
            objectKey = u.ObjectKey
        }));
    }

    private static async Task<IResult> CompleteUploadAsync(
        Guid bucketId,
        CompleteUploadRequest request,
        HttpContext http,
        FileService files,
        CancellationToken cancellationToken)
    {
        var result = await files.CompleteUploadAsync(
                bucketId,
                request.FileId,
                BuildSubject(http.User),
                request.ChecksumSha256,
                cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, f => Results.Ok(new
        {
            id = f.Id,
            bucketId = f.BucketId,
            fileName = f.FileName,
            contentType = f.ContentType,
            sizeBytes = f.SizeBytes,
            status = f.Status.ToString(),
            completedAt = f.CompletedAt
        }));
    }

    private static async Task<IResult> DownloadAsync(
        Guid fileId,
        HttpContext http,
        FileService files,
        CancellationToken cancellationToken)
    {
        var result = await files.CreateDownloadAsync(
                fileId,
                BuildSubject(http.User),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, d => Results.Ok(new
        {
            fileId = d.FileId,
            downloadUrl = d.DownloadUrl.ToString(),
            expiresAt = d.ExpiresAt,
            fileName = d.FileName,
            contentType = d.ContentType
        }));
    }

    private static async Task<IResult> DeleteAsync(
        Guid fileId,
        HttpContext http,
        FileService files,
        CancellationToken cancellationToken)
    {
        var result = await files.SoftDeleteAsync(fileId, BuildSubject(http.User), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToHttpError(result.Error!);
    }

    private static bool TryGetEnvironmentId(HttpContext http, out Guid envId, out IResult? error)
    {
        envId = default;
        error = null;

        if (http.Request.Headers.TryGetValue(EnvironmentHeader, out var header)
            && Guid.TryParse(header.ToString(), out envId))
        {
            return true;
        }

        if (http.Request.Query.TryGetValue("envId", out var query)
            && Guid.TryParse(query.ToString(), out envId))
        {
            return true;
        }

        error = Results.BadRequest(new
        {
            detail = $"Missing environment id. Provide {EnvironmentHeader} header or envId query."
        });
        return false;
    }

    private static AccessSubject BuildSubject(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? "anonymous";

        var roles = user.FindAll("role")
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AccessSubject
        {
            PrincipalId = sub,
            Roles = roles,
            Attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = sub,
                ["email"] = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value
            }
        };
    }

    private static IResult ToHttp<T>(Morph.BuildingBlocks.Results.Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        return ToHttpError(result.Error!);
    }

    private static IResult ToHttpError(Morph.BuildingBlocks.Results.Error error) => error.Code switch
    {
        "not_found" => Results.NotFound(new { detail = error.Message }),
        "forbidden" => Results.Json(new { detail = error.Message }, statusCode: StatusCodes.Status403Forbidden),
        "unauthorized" => Results.Json(new { detail = error.Message }, statusCode: StatusCodes.Status401Unauthorized),
        "validation" => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [error.Message]
        }),
        "conflict" => Results.Conflict(new { detail = error.Message }),
        _ => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest)
    };
}

public sealed record CreateBucketRequest(
    string Slug,
    string? DisplayName = null,
    long? MaxUploadBytes = null,
    IReadOnlyList<string>? AllowedContentTypes = null);

public sealed record CreateUploadRequest(
    string FileName,
    string ContentType,
    long? SizeBytes = null);

public sealed record CompleteUploadRequest(Guid FileId, string? ChecksumSha256 = null);
