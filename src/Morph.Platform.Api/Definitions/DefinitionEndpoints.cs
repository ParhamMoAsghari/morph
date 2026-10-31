using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Services;
using Morph.Infrastructure.Persistence;
using Morph.Messaging.Ports;

namespace Morph.Platform.Api.Definitions;

public static class DefinitionEndpoints
{
    public static IEndpointRouteBuilder MapDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/environments/{environmentId:guid}/definitions")
            .WithTags("Definitions")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{definitionId:guid}", GetDefinitionAsync);
        group.MapGet("/versions/{versionId:guid}", GetVersionAsync);
        group.MapPost("/compose", ComposeAsync);
        group.MapPost("/versions/{versionId:guid}/decompose", DecomposeAsync);
        group.MapPost("/versions/{versionId:guid}/validate", ValidateAsync);
        group.MapPost("/versions/{versionId:guid}/publish", PublishAsync);
        group.MapGet("/versions/{versionId:guid}/effective-model", GetEffectiveModelAsync);
        group.MapPost("/versions/{versionId:guid}/drafts", CreateDraftAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid environmentId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var items = await engine.ListAsync(environmentId, cancellationToken);
        return Results.Ok(items.Select(d => new
        {
            d.DefinitionId,
            d.EnvironmentId,
            d.Slug,
            d.DisplayName,
            kind = d.Kind.ToString(),
            d.CurrentPublishedVersionId,
            d.CreatedAt
        }));
    }

    private static async Task<IResult> CreateAsync(
        Guid environmentId,
        CreateDefinitionRequest request,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var fields = (request.Fields ?? [])
            .Select(f => new FieldDefinition
            {
                FieldId = f.FieldId == Guid.Empty ? Guid.NewGuid() : f.FieldId,
                Slug = f.Slug,
                DisplayName = f.DisplayName,
                DataType = Enum.Parse<FieldDataType>(f.DataType, ignoreCase: true),
                IsRequired = f.IsRequired,
                IsIndexed = f.IsIndexed,
                IsUnique = f.IsUnique,
                Encrypt = f.Encrypt
            })
            .ToList();

        var kind = Enum.Parse<DefinitionKind>(request.Kind, ignoreCase: true);
        var result = await engine.CreateAsync(
            environmentId,
            request.Slug,
            request.DisplayName,
            kind,
            fields,
            cancellationToken);

        return ToHttp(result, tuple => Results.Created(
            $"/v1/environments/{environmentId}/definitions/{tuple.Definition.DefinitionId}",
            new
            {
                tuple.Definition.DefinitionId,
                versionId = tuple.Version.VersionId,
                tuple.Definition.Slug,
                kind = tuple.Definition.Kind.ToString(),
                status = tuple.Version.Status.ToString()
            }));
    }

    private static async Task<IResult> GetDefinitionAsync(
        Guid environmentId,
        Guid definitionId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var definition = await engine.GetDefinitionAsync(definitionId, cancellationToken);
        if (definition is null || definition.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Definition not found." });
        }

        return Results.Ok(definition);
    }

    private static async Task<IResult> GetVersionAsync(
        Guid environmentId,
        Guid versionId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        return Results.Ok(SummarizeVersion(version));
    }

    private static async Task<IResult> ComposeAsync(
        Guid environmentId,
        ComposeDefinitionRequest request,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var uses = (request.ComponentUses ?? [])
            .Select(c => new ComponentUse
            {
                ComponentUseId = c.ComponentUseId == Guid.Empty ? Guid.NewGuid() : c.ComponentUseId,
                Alias = c.Alias,
                TargetDefinitionId = c.TargetDefinitionId,
                PinnedVersionId = c.PinnedVersionId,
                Mode = Enum.Parse<ComponentIncludeMode>(c.Mode, ignoreCase: true),
                IsRequired = c.IsRequired
            })
            .ToList();

        var localFields = (request.LocalFields ?? [])
            .Select(f => new FieldDefinition
            {
                FieldId = f.FieldId == Guid.Empty ? Guid.NewGuid() : f.FieldId,
                Slug = f.Slug,
                DisplayName = f.DisplayName,
                DataType = Enum.Parse<FieldDataType>(f.DataType, ignoreCase: true),
                IsRequired = f.IsRequired,
                IsIndexed = f.IsIndexed,
                IsUnique = f.IsUnique,
                Encrypt = f.Encrypt
            })
            .ToList();

        var resolutions = (request.ConflictResolutions ?? [])
            .Select(r => new ConflictResolution
            {
                ConflictPath = r.ConflictPath,
                WinningFieldId = r.WinningFieldId
            })
            .ToList();

        var kind = Enum.Parse<DefinitionKind>(request.Kind, ignoreCase: true);
        var result = await engine.ComposeAsync(
            environmentId,
            request.Slug,
            request.DisplayName,
            kind,
            uses,
            localFields,
            resolutions,
            cancellationToken);

        return ToHttp(result, tuple => Results.Created(
            $"/v1/environments/{environmentId}/definitions/{tuple.Definition.DefinitionId}",
            new
            {
                tuple.Definition.DefinitionId,
                versionId = tuple.Version.VersionId,
                tuple.Definition.Slug,
                kind = tuple.Definition.Kind.ToString(),
                status = tuple.Version.Status.ToString()
            }));
    }

    private static async Task<IResult> DecomposeAsync(
        Guid environmentId,
        Guid versionId,
        DecomposeRequest request,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        var result = await engine.DecomposeAsync(
            versionId,
            request.FieldIds,
            request.NewComponentSlug,
            request.NewComponentDisplayName,
            request.EmbedAlias,
            request.ForkFields,
            cancellationToken);

        return ToHttp(result, r => Results.Ok(new
        {
            componentDefinitionId = r.ComponentDefinition.DefinitionId,
            componentVersionId = r.ComponentVersion.VersionId,
            updatedSourceVersionId = r.UpdatedSourceVersion.VersionId,
            preservedFieldIds = r.ComponentVersion.Fields.Select(f => f.FieldId).ToArray()
        }));
    }

    private static async Task<IResult> ValidateAsync(
        Guid environmentId,
        Guid versionId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        var issues = await engine.ValidateAsync(versionId, cancellationToken);
        return Results.Ok(new { valid = issues.Count == 0, issues });
    }

    private static async Task<IResult> PublishAsync(
        Guid environmentId,
        Guid versionId,
        EntityDefinitionService engine,
        PlatformDbContext db,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        var result = await engine.PublishAsync(versionId, cancellationToken);
        if (result.IsSuccess)
        {
            var projectId = await db.Environments.AsNoTracking()
                .Where(e => e.Id == environmentId)
                .Select(e => (Guid?)e.ProjectId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            await audit.AppendAsync(
                    new AuditEventEntry
                    {
                        Action = AuditActions.DefinitionPublish,
                        ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                        ProjectId = projectId,
                        EnvironmentId = environmentId,
                        ResourceType = "definition_version",
                        ResourceId = versionId.ToString("D"),
                        DetailJson = $"{{\"slug\":\"{result.Value.Slug}\",\"version\":{result.Value.VersionNumber}}}",
                        OccurredAt = clock.UtcNow
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return ToHttp(result, v => Results.Ok(SummarizeVersion(v)));
    }

    private static async Task<IResult> GetEffectiveModelAsync(
        Guid environmentId,
        Guid versionId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        var result = await engine.CompileAsync(versionId, cancellationToken);
        return ToHttp(result, model => Results.Ok(model));
    }

    private static async Task<IResult> CreateDraftAsync(
        Guid environmentId,
        Guid versionId,
        EntityDefinitionService engine,
        CancellationToken cancellationToken)
    {
        var version = await engine.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.EnvironmentId != environmentId)
        {
            return Results.NotFound(new { detail = "Version not found." });
        }

        var result = await engine.CreateDraftFromPublishedAsync(versionId, cancellationToken);
        return ToHttp(result, v => Results.Created(
            $"/v1/environments/{environmentId}/definitions/versions/{v.VersionId}",
            SummarizeVersion(v)));
    }

    private static object SummarizeVersion(DefinitionVersion version) => new
    {
        version.VersionId,
        version.DefinitionId,
        version.EnvironmentId,
        version.Slug,
        version.DisplayName,
        kind = version.Kind.ToString(),
        version.VersionNumber,
        status = version.Status.ToString(),
        fields = version.Fields,
        componentUses = version.ComponentUses,
        relationships = version.Relationships,
        conflictResolutions = version.ConflictResolutions,
        effectiveModelFingerprint = version.EffectiveModel?.Fingerprint,
        version.CreatedAt,
        version.PublishedAt
    };

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

public sealed record CreateDefinitionRequest(
    string Slug,
    string DisplayName,
    string Kind,
    IReadOnlyList<FieldDto>? Fields);

public sealed record ComposeDefinitionRequest(
    string Slug,
    string DisplayName,
    string Kind,
    IReadOnlyList<ComponentUseDto>? ComponentUses,
    IReadOnlyList<FieldDto>? LocalFields,
    IReadOnlyList<ConflictResolutionDto>? ConflictResolutions);

public sealed record DecomposeRequest(
    IReadOnlyList<Guid> FieldIds,
    string NewComponentSlug,
    string NewComponentDisplayName,
    string EmbedAlias,
    bool ForkFields = false);

public sealed record FieldDto(
    Guid FieldId,
    string Slug,
    string DisplayName,
    string DataType,
    bool IsRequired = false,
    bool IsIndexed = false,
    bool IsUnique = false,
    bool Encrypt = false);

public sealed record ComponentUseDto(
    Guid ComponentUseId,
    string Alias,
    Guid TargetDefinitionId,
    Guid PinnedVersionId,
    string Mode,
    bool IsRequired = true);

public sealed record ConflictResolutionDto(string ConflictPath, Guid WinningFieldId);
