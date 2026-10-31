using System.Security.Cryptography;
using System.Text;
using Morph.BuildingBlocks.Time;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Credentials;
using Morph.Messaging.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Morph.Platform.Api.Credentials;

public static class CredentialEndpoints
{
    public static IEndpointRouteBuilder MapCredentialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/projects/{projectId:guid}/credentials")
            .WithTags("Credentials")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", IssueAsync);
        group.MapPost("/{credentialId:guid}/revoke", RevokeAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.ApiCredentials.AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.KeyPrefix,
                c.EnvironmentId,
                c.CreatedAt,
                c.RevokedAt,
                c.CreatedByPrincipalId
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(items);
    }

    private static async Task<IResult> IssueAsync(
        Guid projectId,
        IssueCredentialRequest request,
        PlatformDbContext db,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."]
            });
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
        if (!projectExists)
        {
            return Results.NotFound(new { detail = "Project not found." });
        }

        var raw = "morph_sk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var prefix = raw[..Math.Min(16, raw.Length)];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var actor = http.User.FindFirst("sub")?.Value;

        var record = new ApiCredentialRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EnvironmentId = request.EnvironmentId,
            Name = request.Name.Trim(),
            KeyPrefix = prefix,
            SecretHash = hash,
            CreatedAt = clock.UtcNow,
            CreatedByPrincipalId = actor
        };

        db.ApiCredentials.Add(record);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.AppendAsync(
                new AuditEventEntry
                {
                    Action = AuditActions.CredentialIssue,
                    ActorPrincipalId = actor,
                    ProjectId = projectId,
                    EnvironmentId = request.EnvironmentId,
                    ResourceType = "api_credential",
                    ResourceId = record.Id.ToString("D"),
                    DetailJson = $"{{\"name\":\"{record.Name}\",\"prefix\":\"{prefix}\"}}",
                    OccurredAt = clock.UtcNow
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"/v1/projects/{projectId}/credentials/{record.Id}",
            new
            {
                record.Id,
                record.Name,
                record.KeyPrefix,
                secret = raw,
                record.EnvironmentId,
                record.CreatedAt
            });
    }

    private static async Task<IResult> RevokeAsync(
        Guid projectId,
        Guid credentialId,
        PlatformDbContext db,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var record = await db.ApiCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return Results.NotFound(new { detail = "Credential not found." });
        }

        if (record.RevokedAt is null)
        {
            record.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await audit.AppendAsync(
                    new AuditEventEntry
                    {
                        Action = AuditActions.CredentialRevoke,
                        ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                        ProjectId = projectId,
                        EnvironmentId = record.EnvironmentId,
                        ResourceType = "api_credential",
                        ResourceId = record.Id.ToString("D"),
                        OccurredAt = clock.UtcNow
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Results.Ok(new { record.Id, record.RevokedAt });
    }
}

public sealed record IssueCredentialRequest(string Name, Guid? EnvironmentId = null);
