using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Morph.BuildingBlocks.Time;
using Morph.Infrastructure.Persistence;
using Morph.Tenancy.Environments;
using Morph.Tenancy.Memberships;
using Morph.Tenancy.Organizations;
using Morph.Tenancy.Projects;

namespace Morph.Platform.Api.Tenancy;

public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var orgs = endpoints.MapGroup("/v1/orgs").WithTags("Tenancy").RequireAuthorization();
        orgs.MapPost("/", CreateOrganizationAsync);
        orgs.MapGet("/", ListOrganizationsAsync);

        var projects = endpoints.MapGroup("/v1/projects").WithTags("Tenancy").RequireAuthorization();
        projects.MapPost("/", CreateProjectAsync);
        projects.MapGet("/", ListProjectsAsync);

        var envs = endpoints.MapGroup("/v1/projects/{projectId:guid}/environments")
            .WithTags("Tenancy")
            .RequireAuthorization();
        envs.MapPost("/", CreateEnvironmentAsync);
        envs.MapGet("/", ListEnvironmentsAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        PlatformDbContext db,
        IClock clock,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name and slug are required."]
            });
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken))
        {
            return Results.Conflict(new { detail = "Organization slug already exists." });
        }

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            CreatedAt = clock.UtcNow
        };

        db.Organizations.Add(org);

        var userIdClaim = httpContext.User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            db.OrganizationMemberships.Add(new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                UserId = userId,
                Role = "org_owner",
                CreatedAt = clock.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/v1/orgs/{org.Id}", org);
    }

    private static async Task<IResult> ListOrganizationsAsync(
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.Organizations
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.Name, o.Slug, o.CreatedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        PlatformDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Slug))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["organizationId, name, and slug are required."]
            });
        }

        var orgExists = await db.Organizations.AnyAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (!orgExists)
        {
            return Results.NotFound(new { detail = "Organization not found." });
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Projects.AnyAsync(
                p => p.OrganizationId == request.OrganizationId && p.Slug == slug,
                cancellationToken))
        {
            return Results.Conflict(new { detail = "Project slug already exists in this organization." });
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Slug = slug,
            CreatedAt = clock.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/v1/projects/{project.Id}", project);
    }

    private static async Task<IResult> ListProjectsAsync(
        Guid? organizationId,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking();
        if (organizationId is not null)
        {
            query = query.Where(p => p.OrganizationId == organizationId);
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.OrganizationId, p.Name, p.Slug, p.CreatedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateEnvironmentAsync(
        Guid projectId,
        CreateEnvironmentRequest request,
        PlatformDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return Results.NotFound(new { detail = "Project not found." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["name and slug are required."]
            });
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Environments.AnyAsync(e => e.ProjectId == projectId && e.Slug == slug, cancellationToken))
        {
            return Results.Conflict(new { detail = "Environment slug already exists in this project." });
        }

        var env = new AppEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Slug = slug,
            SchemaName = $"p_{projectId:N}_{slug}".Replace('-', '_'),
            CreatedAt = clock.UtcNow
        };

        db.Environments.Add(env);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/v1/projects/{projectId}/environments/{env.Id}", env);
    }

    private static async Task<IResult> ListEnvironmentsAsync(
        Guid projectId,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.Environments
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.ProjectId, e.Name, e.Slug, e.SchemaName, e.CreatedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }
}

public sealed record CreateOrganizationRequest(string Name, string Slug);

public sealed record CreateProjectRequest(Guid OrganizationId, string Name, string Slug);

public sealed record CreateEnvironmentRequest(string Name, string Slug);
