using System.Text.Json;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Services;
using Morph.Infrastructure.Persistence;
using Morph.Messaging.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Morph.Platform.Api.Backup;

public static class DefinitionBackupEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static IEndpointRouteBuilder MapDefinitionBackupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/environments/{environmentId:guid}/backup")
            .WithTags("Backup")
            .RequireAuthorization();

        group.MapGet("/definitions", ExportAsync);
        group.MapPost("/definitions/import", ImportAsync);
        return endpoints;
    }

    private static async Task<IResult> ExportAsync(
        Guid environmentId,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var envExists = await db.Environments.AnyAsync(e => e.Id == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (!envExists)
        {
            return Results.NotFound(new { detail = "Environment not found." });
        }

        var definitions = await db.EntityDefinitions.AsNoTracking()
            .Where(d => d.EnvironmentId == environmentId)
            .OrderBy(d => d.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var versionIds = definitions
            .Select(d => d.CurrentPublishedVersionId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();

        var versions = await db.DefinitionVersions.AsNoTracking()
            .Where(v => v.EnvironmentId == environmentId)
            .Where(v => versionIds.Contains(v.VersionId) || v.Status == nameof(DefinitionVersionStatus.Draft))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = new DefinitionBackupDocument
        {
            Format = "morph.definitions.v1",
            ExportedAt = DateTimeOffset.UtcNow,
            EnvironmentId = environmentId,
            Definitions = definitions.Select(d => new DefinitionBackupEntry
            {
                DefinitionId = d.DefinitionId,
                Slug = d.Slug,
                DisplayName = d.DisplayName,
                Kind = d.Kind,
                CurrentPublishedVersionId = d.CurrentPublishedVersionId,
                Versions = versions
                    .Where(v => v.DefinitionId == d.DefinitionId)
                    .OrderBy(v => v.VersionNumber)
                    .Select(v => new DefinitionVersionBackupEntry
                    {
                        VersionId = v.VersionId,
                        VersionNumber = v.VersionNumber,
                        Status = v.Status,
                        AuthoringJson = v.AuthoringJson,
                        EffectiveModelJson = v.EffectiveModelJson,
                        PublishedAt = v.PublishedAt
                    })
                    .ToList()
            }).ToList()
        };

        return Results.Json(payload, JsonOptions);
    }

    private static async Task<IResult> ImportAsync(
        Guid environmentId,
        DefinitionBackupDocument document,
        EntityDefinitionService engine,
        PlatformDbContext db,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (document.Definitions is null || document.Definitions.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["definitions"] = ["Backup document must include at least one definition."]
            });
        }

        var env = await db.Environments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (env is null)
        {
            return Results.NotFound(new { detail = "Environment not found." });
        }

        var imported = 0;
        var skipped = 0;

        foreach (var entry in document.Definitions)
        {
            if (string.IsNullOrWhiteSpace(entry.Slug) || string.IsNullOrWhiteSpace(entry.Kind))
            {
                skipped++;
                continue;
            }

            var existing = await db.EntityDefinitions.AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.EnvironmentId == environmentId && d.Slug == entry.Slug,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                skipped++;
                continue;
            }

            if (!Enum.TryParse<DefinitionKind>(entry.Kind, ignoreCase: true, out var kind))
            {
                skipped++;
                continue;
            }

            var fields = ExtractFields(entry);
            var result = await engine.CreateAsync(
                    environmentId,
                    entry.Slug,
                    entry.DisplayName ?? entry.Slug,
                    kind,
                    fields,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                imported++;
            }
            else
            {
                skipped++;
            }
        }

        await audit.AppendAsync(
                new AuditEventEntry
                {
                    Action = AuditActions.DefinitionBackupImport,
                    ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                    ProjectId = env.ProjectId,
                    EnvironmentId = environmentId,
                    ResourceType = "definition_backup",
                    ResourceId = environmentId.ToString("D"),
                    DetailJson = $"{{\"imported\":{imported},\"skipped\":{skipped}}}",
                    OccurredAt = clock.UtcNow
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new { imported, skipped, environmentId });
    }

    private static List<FieldDefinition> ExtractFields(DefinitionBackupEntry entry)
    {
        var latest = entry.Versions?
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (latest is null || string.IsNullOrWhiteSpace(latest.AuthoringJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(latest.AuthoringJson);
            if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl)
                || fieldsEl.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<FieldDefinition>();
            foreach (var f in fieldsEl.EnumerateArray())
            {
                var slug = f.TryGetProperty("slug", out var s) ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                var dataType = FieldDataType.String;
                if (f.TryGetProperty("dataType", out var dt) && dt.GetString() is { } dts)
                {
                    Enum.TryParse(dts, ignoreCase: true, out dataType);
                }

                list.Add(new FieldDefinition
                {
                    FieldId = f.TryGetProperty("fieldId", out var fid) && Guid.TryParse(fid.GetString(), out var g)
                        ? g
                        : Guid.NewGuid(),
                    Slug = slug,
                    DisplayName = f.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? slug : slug,
                    DataType = dataType,
                    IsRequired = f.TryGetProperty("isRequired", out var req) && req.ValueKind == JsonValueKind.True,
                    IsIndexed = f.TryGetProperty("isIndexed", out var idx) && idx.ValueKind == JsonValueKind.True,
                    IsUnique = f.TryGetProperty("isUnique", out var uq) && uq.ValueKind == JsonValueKind.True
                });
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed class DefinitionBackupDocument
{
    public string Format { get; set; } = "morph.definitions.v1";

    public DateTimeOffset ExportedAt { get; set; }

    public Guid EnvironmentId { get; set; }

    public List<DefinitionBackupEntry> Definitions { get; set; } = [];
}

public sealed class DefinitionBackupEntry
{
    public Guid DefinitionId { get; set; }

    public required string Slug { get; set; }

    public string? DisplayName { get; set; }

    public required string Kind { get; set; }

    public Guid? CurrentPublishedVersionId { get; set; }

    public List<DefinitionVersionBackupEntry> Versions { get; set; } = [];
}

public sealed class DefinitionVersionBackupEntry
{
    public Guid VersionId { get; set; }

    public int VersionNumber { get; set; }

    public required string Status { get; set; }

    public required string AuthoringJson { get; set; }

    public string? EffectiveModelJson { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
