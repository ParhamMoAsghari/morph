using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Morph.Access.Domain;
using Morph.Access.Services;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.Messaging.Ports;

namespace Morph.Platform.Api.Access;

public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/projects/{projectId:guid}")
            .WithTags("Access")
            .RequireAuthorization();

        group.MapGet("/roles", ListRolesAsync);
        group.MapPost("/roles", CreateRoleAsync);
        group.MapGet("/role-bindings", ListBindingsAsync);
        group.MapPost("/role-bindings", BindRoleAsync);
        group.MapGet("/policies", ListPoliciesAsync);
        group.MapPost("/policies", CreatePolicyAsync);
        group.MapPost("/policies/{policyId:guid}/publish", PublishPolicyAsync);

        return endpoints;
    }

    private static async Task<IResult> ListRolesAsync(
        Guid projectId,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        await access.EnsureBuiltInRolesAsync(projectId, cancellationToken);
        var roles = await access.ListRolesAsync(projectId, cancellationToken);
        return Results.Ok(roles);
    }

    private static async Task<IResult> CreateRoleAsync(
        Guid projectId,
        CreateRoleRequest request,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        var result = await access.CreateRoleAsync(
            projectId,
            request.Name,
            request.DisplayName,
            request.Inherits,
            cancellationToken);
        return ToHttp(result, r => Results.Created($"/v1/projects/{projectId}/roles/{r.RoleId}", r));
    }

    private static async Task<IResult> ListBindingsAsync(
        Guid projectId,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        var items = await access.ListBindingsAsync(projectId, cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> BindRoleAsync(
        Guid projectId,
        BindRoleRequest request,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        var result = await access.BindRoleAsync(
            projectId,
            request.PrincipalId,
            request.RoleName,
            request.EnvironmentId,
            cancellationToken);
        return ToHttp(result, b => Results.Created($"/v1/projects/{projectId}/role-bindings/{b.BindingId}", b));
    }

    private static async Task<IResult> ListPoliciesAsync(
        Guid projectId,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        var items = await access.ListPoliciesAsync(projectId, cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> CreatePolicyAsync(
        Guid projectId,
        CreatePolicyRequest request,
        AccessAdminService access,
        CancellationToken cancellationToken)
    {
        var effect = Enum.Parse<AccessEffect>(request.Effect, ignoreCase: true);
        ConditionExpression? condition = null;
        if (request.Condition is { } conditionJson && conditionJson.ValueKind == JsonValueKind.Object)
        {
            condition = ConditionJsonParser.Parse(conditionJson);
        }

        var draft = new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = projectId,
            EnvironmentId = request.EnvironmentId,
            Name = request.Name,
            Effect = effect,
            Actions = request.Actions,
            Resources = request.Resources,
            Condition = condition,
            Priority = request.Priority,
            FieldMaskOnly = request.FieldMaskOnly,
            EmitRowFilterObligation = request.EmitRowFilterObligation
        };

        var result = await access.CreatePolicyAsync(draft, cancellationToken);
        return ToHttp(result, p => Results.Created($"/v1/projects/{projectId}/policies/{p.PolicyId}", p));
    }

    private static async Task<IResult> PublishPolicyAsync(
        Guid projectId,
        Guid policyId,
        AccessAdminService access,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await access.PublishPolicyAsync(policyId, cancellationToken);
        if (result.IsSuccess)
        {
            await audit.AppendAsync(
                    new AuditEventEntry
                    {
                        Action = AuditActions.PolicyPublish,
                        ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                        ProjectId = projectId,
                        ResourceType = "policy",
                        ResourceId = policyId.ToString("D"),
                        DetailJson = $"{{\"name\":\"{result.Value.Name}\"}}",
                        OccurredAt = clock.UtcNow
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return ToHttp(result, p => Results.Ok(p));
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

public sealed record CreateRoleRequest(string Name, string? DisplayName, IReadOnlyList<string>? Inherits);

public sealed record BindRoleRequest(string PrincipalId, string RoleName, Guid? EnvironmentId = null);

public sealed record CreatePolicyRequest(
    string Name,
    string Effect,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Resources,
    JsonElement? Condition = null,
    int Priority = 0,
    Guid? EnvironmentId = null,
    bool FieldMaskOnly = false,
    bool EmitRowFilterObligation = false);

public static class ConditionJsonParser
{
    public static ConditionExpression Parse(JsonElement el)
    {
        if (el.TryGetProperty("and", out var andEl))
        {
            return ConditionExpression.And(andEl.EnumerateArray().Select(Parse).ToArray());
        }

        if (el.TryGetProperty("or", out var orEl))
        {
            return ConditionExpression.Or(orEl.EnumerateArray().Select(Parse).ToArray());
        }

        if (el.TryGetProperty("not", out var notEl))
        {
            return ConditionExpression.Not(Parse(notEl));
        }

        var op = el.GetProperty("op").GetString() ?? "eq";
        return new ConditionExpression
        {
            Op = op,
            Left = ParseOperand(el.GetProperty("left")),
            Right = el.TryGetProperty("right", out var right) ? ParseOperand(right) : null
        };
    }

    private static ConditionOperand ParseOperand(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("attr", out var attr))
        {
            return ConditionOperand.Attr(attr.GetString()!);
        }

        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("literal", out var lit))
        {
            return ConditionOperand.Literal(lit.ValueKind switch
            {
                JsonValueKind.String => lit.GetString(),
                JsonValueKind.Number when lit.TryGetInt64(out var n) => n,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => lit.GetRawText()
            });
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString()!;
            return s.Contains('.') ? ConditionOperand.Attr(s) : ConditionOperand.Literal(s);
        }

        return ConditionOperand.Literal(el.GetRawText());
    }
}
