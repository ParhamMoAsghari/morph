using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Results;
using Morph.Infrastructure.Persistence;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Services;

namespace Morph.Platform.Api.Processes;

public static class ProcessEndpoints
{
    public static IEndpointRouteBuilder MapProcessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/environments/{environmentId:guid}/processes")
            .WithTags("Processes")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", UpsertAsync);
        group.MapPost("/push", UpsertAsync);
        group.MapGet("/{definitionId:guid}", GetDefinitionAsync);
        group.MapGet("/versions/{versionId:guid}", GetVersionAsync);
        group.MapGet("/versions/{versionId:guid}/authoring", GetAuthoringAsync);
        group.MapPost("/versions/{versionId:guid}/validate", ValidateAsync);
        group.MapPost("/versions/{versionId:guid}/publish", PublishAsync);
        group.MapPost("/{slug}/run", RunAsync);
        group.MapGet("/instances/{instanceId:guid}", GetInstanceAsync);
        group.MapGet("/instances/{instanceId:guid}/logs", GetLogsAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid environmentId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var items = await definitions.ListAsync(environmentId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(items.Select(d => new
        {
            d.DefinitionId,
            d.EnvironmentId,
            d.ProjectId,
            d.Slug,
            d.DisplayName,
            d.CurrentPublishedVersionId,
            d.CreatedAt,
            d.UpdatedAt
        }));
    }

    private static async Task<IResult> UpsertAsync(
        Guid environmentId,
        ProcessUpsertRequest request,
        ProcessDefinitionService definitions,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var env = await db.Environments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (env is null)
        {
            return Results.NotFound(new { detail = "Environment not found." });
        }

        ProcessDocument document;
        try
        {
            document = ResolveDocument(request);
        }
        catch (Exception ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["document"] = [ex.Message]
            });
        }

        var result = await definitions.UpsertDraftAsync(
                environmentId,
                env.ProjectId,
                document,
                cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, tuple => Results.Ok(new
        {
            tuple.Definition.DefinitionId,
            versionId = tuple.Version.VersionId,
            tuple.Definition.Slug,
            status = tuple.Version.Status.ToString(),
            tuple.Version.VersionNumber
        }));
    }

    private static async Task<IResult> GetDefinitionAsync(
        Guid environmentId,
        Guid definitionId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var definition = await definitions.GetDefinitionAsync(definitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null || definition.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process definition not found." });
        }

        return Results.Ok(definition);
    }

    private static async Task<IResult> GetVersionAsync(
        Guid environmentId,
        Guid versionId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var version = await definitions.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process version not found." });
        }

        return Results.Ok(new
        {
            version.VersionId,
            version.DefinitionId,
            version.EnvironmentId,
            version.Slug,
            version.DisplayName,
            version.VersionNumber,
            status = version.Status.ToString(),
            version.CreatedAt,
            version.PublishedAt
        });
    }

    private static async Task<IResult> GetAuthoringAsync(
        Guid environmentId,
        Guid versionId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var version = await definitions.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process version not found." });
        }

        var result = await definitions.GetAuthoringAsync(versionId, cancellationToken).ConfigureAwait(false);
        return ToHttp(result, Results.Ok);
    }

    private static async Task<IResult> ValidateAsync(
        Guid environmentId,
        Guid versionId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var version = await definitions.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process version not found." });
        }

        var result = await definitions.ValidateAsync(versionId, cancellationToken).ConfigureAwait(false);
        return ToHttp(result, Results.Ok);
    }

    private static async Task<IResult> PublishAsync(
        Guid environmentId,
        Guid versionId,
        ProcessDefinitionService definitions,
        CancellationToken cancellationToken)
    {
        var version = await definitions.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process version not found." });
        }

        var result = await definitions.PublishAsync(versionId, cancellationToken).ConfigureAwait(false);
        return ToHttp(result, v => Results.Ok(new
        {
            v.VersionId,
            v.DefinitionId,
            v.Slug,
            status = v.Status.ToString(),
            v.VersionNumber,
            v.PublishedAt
        }));
    }

    private static async Task<IResult> RunAsync(
        Guid environmentId,
        string slug,
        ProcessRunRequest? request,
        ProcessRuntimeService runtime,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var subject = BuildSubject(http.User);
        var result = await runtime.StartAsync(
                environmentId,
                slug,
                request?.Input,
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, instance => Results.Accepted(
            $"/v1/environments/{environmentId}/processes/instances/{instance.InstanceId}",
            SummarizeInstance(instance)));
    }

    private static async Task<IResult> GetInstanceAsync(
        Guid environmentId,
        Guid instanceId,
        ProcessRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        var instance = await runtime.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null || instance.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process instance not found." });
        }

        return Results.Ok(SummarizeInstance(instance));
    }

    private static async Task<IResult> GetLogsAsync(
        Guid environmentId,
        Guid instanceId,
        ProcessRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        var instance = await runtime.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null || instance.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Process instance not found." });
        }

        var logs = await runtime.GetLogsAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(logs);
    }

    private static ProcessDocument ResolveDocument(ProcessUpsertRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Yaml))
        {
            return ProcessIrSerializer.ParseYaml(request.Yaml);
        }

        if (request.Document is { } doc)
        {
            return doc;
        }

        if (request.DocumentJson is { } json)
        {
            return ProcessIrSerializer.ParseJson(json);
        }

        throw new InvalidOperationException("Provide yaml, document, or documentJson.");
    }

    private static object SummarizeInstance(ProcessInstance instance) => new
    {
        instance.InstanceId,
        instance.DefinitionId,
        instance.VersionId,
        instance.EnvironmentId,
        instance.ProjectId,
        instance.Slug,
        status = instance.Status.ToString(),
        instance.FailureMessage,
        instance.WaitingUntil,
        instance.CreatedAt,
        instance.UpdatedAt,
        instance.CompletedAt
    };

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

    private static IResult ToHttp<T>(Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        var error = result.Error!;
        return error.Code switch
        {
            "not_found" => Results.NotFound(new { detail = error.Message }),
            "conflict" => Results.Conflict(new { detail = error.Message }),
            "validation" => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [error.Message]
            }),
            _ => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}

public sealed class ProcessUpsertRequest
{
    public string? Yaml { get; set; }

    public string? DocumentJson { get; set; }

    public ProcessDocument? Document { get; set; }
}

public sealed class ProcessRunRequest
{
    public Dictionary<string, object?>? Input { get; set; }
}
