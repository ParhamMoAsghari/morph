using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.DataPlane.Cache;
using Morph.EntityEngine.Services;
using Morph.Infrastructure.Persistence;
using Morph.Messaging.Ports;

namespace Morph.Platform.Api.Migrations;

public static class MigrationEndpoints
{
    public static IEndpointRouteBuilder MapMigrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/environments/{environmentId:guid}/migrations")
            .WithTags("Migrations")
            .RequireAuthorization();

        group.MapPost("/plan", PlanAsync);
        group.MapPost("/apply", ApplyAsync);
        group.MapPost("/{runId:guid}/resume", ResumeAsync);

        return endpoints;
    }

    private static async Task<IResult> PlanAsync(
        Guid environmentId,
        PlanMigrationRequest request,
        MigrationService migrations,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var schema = await ResolveSchemaAsync(environmentId, db, cancellationToken);
        if (schema is null)
        {
            return Results.NotFound(new { detail = "Environment not found or missing schema name." });
        }

        if (request.FromVersionId is null || request.FromVersionId == Guid.Empty)
        {
            var result = await migrations.PlanCreateAsync(request.ToVersionId, schema, cancellationToken);
            return ToHttp(result, tuple => Results.Ok(new
            {
                planId = tuple.Plan.PlanId,
                tuple.Plan.DefinitionId,
                tuple.Plan.FromVersionId,
                tuple.Plan.ToVersionId,
                stages = tuple.Plan.Steps.Select(s => new
                {
                    s.Ordinal,
                    kind = s.Kind.ToString(),
                    stage = s.Stage.ToString(),
                    s.Sql
                }),
                mappingFingerprint = tuple.Mapping.Fingerprint,
                table = tuple.Mapping.QualifiedTableName
            }));
        }

        var evolve = await migrations.PlanEvolveAsync(
            request.FromVersionId.Value,
            request.ToVersionId,
            schema,
            cancellationToken);

        return ToHttp(evolve, tuple => Results.Ok(new
        {
            planId = tuple.Plan.PlanId,
            tuple.Plan.DefinitionId,
            tuple.Plan.FromVersionId,
            tuple.Plan.ToVersionId,
            stages = tuple.Plan.Steps.Select(s => new
            {
                s.Ordinal,
                kind = s.Kind.ToString(),
                stage = s.Stage.ToString(),
                s.Sql
            }),
            mappingFingerprint = tuple.Mapping.Fingerprint,
            table = tuple.Mapping.QualifiedTableName
        }));
    }

    private static async Task<IResult> ApplyAsync(
        Guid environmentId,
        ApplyMigrationRequest request,
        MigrationService migrations,
        PlatformDbContext db,
        IPublishedModelCache cache,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var schema = await ResolveSchemaAsync(environmentId, db, cancellationToken);
        if (schema is null)
        {
            return Results.NotFound(new { detail = "Environment not found or missing schema name." });
        }

        Result<(Morph.EntityEngine.Domain.MigrationPlan Plan, Morph.EntityEngine.Domain.StorageMapping Mapping)> planResult;
        if (request.FromVersionId is null || request.FromVersionId == Guid.Empty)
        {
            planResult = await migrations.PlanCreateAsync(request.ToVersionId, schema, cancellationToken);
        }
        else
        {
            planResult = await migrations.PlanEvolveAsync(
                request.FromVersionId.Value,
                request.ToVersionId,
                schema,
                cancellationToken);
        }

        if (planResult.IsFailure)
        {
            return ToHttpError(planResult.Error!);
        }

        var apply = await migrations.ApplyAsync(
            planResult.Value.Plan,
            request.PriorPublishedVersionId,
            cancellationToken);

        if (apply.IsSuccess)
        {
            cache.InvalidateEnvironment(environmentId);
            cache.InvalidateDefinition(environmentId, planResult.Value.Plan.DefinitionId);

            var projectId = await db.Environments.AsNoTracking()
                .Where(e => e.Id == environmentId)
                .Select(e => (Guid?)e.ProjectId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            await audit.AppendAsync(
                    new AuditEventEntry
                    {
                        Action = AuditActions.MigrationApply,
                        ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                        ProjectId = projectId,
                        EnvironmentId = environmentId,
                        ResourceType = "migration_run",
                        ResourceId = apply.Value.RunId.ToString("D"),
                        DetailJson =
                            $"{{\"definitionId\":\"{apply.Value.DefinitionId}\",\"toVersionId\":\"{apply.Value.ToVersionId}\"}}",
                        OccurredAt = clock.UtcNow
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return ToHttp(apply, run => Results.Ok(new
        {
            run.RunId,
            run.PlanId,
            run.DefinitionId,
            run.FromVersionId,
            run.ToVersionId,
            status = run.Status.ToString(),
            run.LastCompletedStepOrdinal,
            run.FailureMessage,
            run.CompletedAt
        }));
    }

    private static async Task<IResult> ResumeAsync(
        Guid environmentId,
        Guid runId,
        MigrationService migrations,
        IPublishedModelCache cache,
        CancellationToken cancellationToken)
    {
        var result = await migrations.ResumeAsync(runId, cancellationToken);
        if (result.IsSuccess)
        {
            cache.InvalidateEnvironment(environmentId);
            cache.InvalidateDefinition(environmentId, result.Value.DefinitionId);
        }

        return ToHttp(result, run => Results.Ok(new
        {
            run.RunId,
            status = run.Status.ToString(),
            run.LastCompletedStepOrdinal,
            run.FailureMessage,
            run.CompletedAt
        }));
    }

    private static async Task<string?> ResolveSchemaAsync(
        Guid environmentId,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var env = await db.Environments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken);
        return env?.SchemaName;
    }

    private static IResult ToHttp<T>(Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        return ToHttpError(result.Error!);
    }

    private static IResult ToHttpError(Error error) => error.Code switch
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

public sealed record PlanMigrationRequest(Guid ToVersionId, Guid? FromVersionId = null);

public sealed record ApplyMigrationRequest(
    Guid ToVersionId,
    Guid? FromVersionId = null,
    Guid? PriorPublishedVersionId = null);
