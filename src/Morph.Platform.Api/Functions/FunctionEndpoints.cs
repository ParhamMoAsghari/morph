using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Morph.BuildingBlocks.Results;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Services;
using Morph.Infrastructure.Persistence;

namespace Morph.Platform.Api.Functions;

public static class FunctionEndpoints
{
    public static IEndpointRouteBuilder MapFunctionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/environments/{environmentId:guid}/functions")
            .WithTags("Functions")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", RegisterAsync);
        group.MapPost("/{slug}/invoke", InvokeAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid environmentId,
        FunctionRuntimeService functions,
        CancellationToken cancellationToken)
    {
        var items = await functions.ListAsync(environmentId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(items.Select(d => new
        {
            d.FunctionId,
            d.EnvironmentId,
            d.ProjectId,
            d.Slug,
            d.DisplayName,
            runtime = d.Runtime.ToString(),
            trigger = d.Trigger.ToString(),
            d.CreatedAt,
            d.UpdatedAt
        }));
    }

    private static async Task<IResult> RegisterAsync(
        Guid environmentId,
        RegisterFunctionRequest request,
        FunctionRuntimeService functions,
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

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["slug"] = ["Function slug is required."]
            });
        }

        var runtime = ParseRuntime(request.Runtime);
        var trigger = ParseTrigger(request.Trigger);

        var result = await functions.RegisterAsync(
                environmentId,
                env.ProjectId,
                request.Slug,
                request.DisplayName,
                runtime,
                trigger,
                request.Source,
                cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, d => Results.Ok(new
        {
            d.FunctionId,
            d.EnvironmentId,
            d.ProjectId,
            d.Slug,
            d.DisplayName,
            runtime = d.Runtime.ToString(),
            trigger = d.Trigger.ToString(),
            d.CreatedAt,
            d.UpdatedAt
        }));
    }

    private static async Task<IResult> InvokeAsync(
        Guid environmentId,
        string slug,
        InvokeFunctionRequest? request,
        FunctionRuntimeService functions,
        CancellationToken cancellationToken)
    {
        var result = await functions
            .InvokeAsync(environmentId, slug, request?.Input, cancellationToken)
            .ConfigureAwait(false);

        return ToHttp(result, r => Results.Ok(new
        {
            r.FunctionSlug,
            r.Succeeded,
            r.Output,
            r.Error,
            durationMs = r.Duration.TotalMilliseconds
        }));
    }

    private static FunctionRuntimeKind ParseRuntime(string? value) =>
        string.Equals(value, "dotnet", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "dotnetstub", StringComparison.OrdinalIgnoreCase)
            ? FunctionRuntimeKind.DotNetStub
            : FunctionRuntimeKind.TypeScriptStub;

    private static FunctionTriggerKind ParseTrigger(string? value)
    {
        if (string.Equals(value, "lifecycle", StringComparison.OrdinalIgnoreCase))
        {
            return FunctionTriggerKind.Lifecycle;
        }

        if (string.Equals(value, "process", StringComparison.OrdinalIgnoreCase))
        {
            return FunctionTriggerKind.Process;
        }

        return FunctionTriggerKind.Http;
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
            "validation" => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [error.Message]
            }),
            _ => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}

public sealed class RegisterFunctionRequest
{
    public string? Slug { get; init; }

    public string? DisplayName { get; init; }

    public string? Runtime { get; init; }

    public string? Trigger { get; init; }

    public string? Source { get; init; }
}

public sealed class InvokeFunctionRequest
{
    public object? Input { get; init; }
}
