using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Query;
using Morph.DataPlane.Records;
using Morph.DataPlane.Services;
using Morph.Runtime.Api.Auth;
using Morph.Runtime.Api.Realtime;

namespace Morph.Runtime.Api.Data;

public static class DataEndpoints
{
    public const string EnvironmentHeader = RuntimeRequestContext.EnvironmentHeader;

    public static IEndpointRouteBuilder MapDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Reads are optional-auth: unauthenticated callers enter as the Access "anon"
        // principal and succeed only when published Allow policies permit list/read.
        // Mutations stay authenticated; Access still enforces for every principal.
        var group = endpoints.MapGroup("/data/v1")
            .WithTags("Data");

        group.MapGet("/_meta/status", () => Results.Ok(new
        {
            status = "ready",
            phase = 7,
            message = "Dynamic CRUD with Morph.Access (RBAC+ABAC+PBAC). GraphQL is the primary app data API; REST remains."
        }));

        group.MapGet("/{entity}", ListAsync);
        group.MapGet("/{entity}/{id:guid}", GetAsync);

        group.MapPost("/{entity}", CreateAsync).RequireAuthorization();
        group.MapPatch("/{entity}/{id:guid}", UpdateAsync).RequireAuthorization();
        group.MapDelete("/{entity}/{id:guid}", DeleteAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string entity,
        HttpContext http,
        DataPlaneService data,
        string? filter,
        string? sort,
        int? pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var subject = RuntimeRequestContext.BuildSubject(http.User);
        FilterNode? filterNode = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                filterNode = FilterJsonParser.Parse(filter);
            }
            catch (Exception ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["filter"] = [ex.Message]
                });
            }
        }

        IReadOnlyList<SortSpec>? sorts = null;
        if (!string.IsNullOrWhiteSpace(sort))
        {
            sorts = sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var desc = part.StartsWith('-');
                    var path = desc ? part[1..] : part;
                    return new SortSpec(path, desc);
                })
                .ToList();
        }

        var result = await data.ListAsync(
            envId,
            entity,
            new DynamicListRequest
            {
                Filter = filterNode,
                Sort = sorts,
                PageSize = pageSize ?? 50,
                Cursor = cursor
            },
            subject,
            cancellationToken);

        return ToHttp(result, page => Results.Ok(new
        {
            items = page.Items.Select(ToDto),
            nextCursor = page.NextCursor,
            hasMore = page.HasMore
        }));
    }

    private static async Task<IResult> CreateAsync(
        string entity,
        HttpContext http,
        DataPlaneService data,
        IEnvironmentSchemaResolver envs,
        EntityEventPublisher events,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var fields = JsonElementToDictionary(body);
        var result = await data.CreateAsync(envId, entity, fields, RuntimeRequestContext.BuildSubject(http.User), cancellationToken);
        if (result.IsSuccess)
        {
            await PublishMutationAsync(
                    envs,
                    events,
                    envId,
                    entity,
                    "record.created",
                    result.Value.Id,
                    ToDto(result.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return ToHttp(result, doc => Results.Created($"/data/v1/{entity}/{doc.Id}", ToDto(doc)));
    }

    private static async Task<IResult> GetAsync(
        string entity,
        Guid id,
        HttpContext http,
        DataPlaneService data,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var result = await data.GetAsync(envId, entity, id, RuntimeRequestContext.BuildSubject(http.User), cancellationToken);
        return ToHttp(result, doc => Results.Ok(ToDto(doc)));
    }

    private static async Task<IResult> UpdateAsync(
        string entity,
        Guid id,
        HttpContext http,
        DataPlaneService data,
        IEnvironmentSchemaResolver envs,
        EntityEventPublisher events,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var fields = JsonElementToDictionary(body);
        var result = await data.UpdateAsync(envId, entity, id, fields, RuntimeRequestContext.BuildSubject(http.User), cancellationToken);
        if (result.IsSuccess)
        {
            await PublishMutationAsync(
                    envs,
                    events,
                    envId,
                    entity,
                    "record.updated",
                    result.Value.Id,
                    ToDto(result.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return ToHttp(result, doc => Results.Ok(ToDto(doc)));
    }

    private static async Task<IResult> DeleteAsync(
        string entity,
        Guid id,
        HttpContext http,
        DataPlaneService data,
        IEnvironmentSchemaResolver envs,
        EntityEventPublisher events,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvironmentId(http, out var envId, out var error))
        {
            return error!;
        }

        var result = await data.DeleteAsync(envId, entity, id, RuntimeRequestContext.BuildSubject(http.User), cancellationToken);
        if (result.IsSuccess)
        {
            await PublishMutationAsync(
                    envs,
                    events,
                    envId,
                    entity,
                    "record.deleted",
                    id,
                    new { id, entity },
                    cancellationToken)
                .ConfigureAwait(false);
            return Results.NoContent();
        }

        return ToHttpError(result.Error!);
    }

    private static async Task PublishMutationAsync(
        IEnvironmentSchemaResolver envs,
        EntityEventPublisher events,
        Guid envId,
        string entity,
        string eventType,
        Guid? recordId,
        object body,
        CancellationToken cancellationToken)
    {
        var env = await envs.ResolveAsync(envId, cancellationToken).ConfigureAwait(false);
        if (env is null)
        {
            return;
        }

        await events
            .PublishAsync(env.ProjectId, envId, entity, eventType, recordId, body, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryGetEnvironmentId(HttpContext http, out Guid envId, out IResult? error)
    {
        if (RuntimeRequestContext.TryGetEnvironmentId(http, out envId, out var detail))
        {
            error = null;
            return true;
        }

        error = Results.BadRequest(new { detail });
        return false;
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement body)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (body.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var prop in body.EnumerateObject())
        {
            map[prop.Name] = JsonValue(prop.Value);
        }

        return map;
    }

    private static object? JsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number when el.TryGetInt64(out var l) => l,
        JsonValueKind.Number when el.TryGetDecimal(out var d) => d,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object or JsonValueKind.Array => el.GetRawText(),
        _ => el.ToString()
    };

    private static object ToDto(EntityRecordDocument doc) => new
    {
        id = doc.Id,
        definitionVersionId = doc.DefinitionVersionId,
        createdAt = doc.CreatedAt,
        updatedAt = doc.UpdatedAt,
        createdBy = doc.CreatedBy,
        updatedBy = doc.UpdatedBy,
        rowVersion = doc.RowVersion,
        fields = doc.Fields
    };

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

/// <summary>
/// Parses a small JSON filter document into the allowlisted AST.
/// Shape: { "and": [ ... ] } | { "or": [ ... ] } | { "not": {...} } | { "field": "title", "op": "eq", "value": "x" }
/// </summary>
public static class FilterJsonParser
{
    public static FilterNode Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseNode(doc.RootElement);
    }

    private static FilterNode ParseNode(JsonElement el)
    {
        if (el.TryGetProperty("and", out var andEl) && andEl.ValueKind == JsonValueKind.Array)
        {
            return new FilterAnd(andEl.EnumerateArray().Select(ParseNode).ToList());
        }

        if (el.TryGetProperty("or", out var orEl) && orEl.ValueKind == JsonValueKind.Array)
        {
            return new FilterOr(orEl.EnumerateArray().Select(ParseNode).ToList());
        }

        if (el.TryGetProperty("not", out var notEl))
        {
            return new FilterNot(ParseNode(notEl));
        }

        var field = el.GetProperty("field").GetString()
                    ?? throw new InvalidOperationException("Filter requires 'field'.");
        var opText = el.TryGetProperty("op", out var opEl)
            ? opEl.GetString() ?? "eq"
            : "eq";

        var op = opText.Trim().ToLowerInvariant() switch
        {
            "eq" or "=" => FilterOperator.Eq,
            "neq" or "!=" or "<>" => FilterOperator.Neq,
            "gt" or ">" => FilterOperator.Gt,
            "gte" or ">=" => FilterOperator.Gte,
            "lt" or "<" => FilterOperator.Lt,
            "lte" or "<=" => FilterOperator.Lte,
            "in" => FilterOperator.In,
            "contains" or "ilike" => FilterOperator.Contains,
            "isnull" or "is_null" => FilterOperator.IsNull,
            "isnotnull" or "is_not_null" => FilterOperator.IsNotNull,
            _ => throw new InvalidOperationException($"Operator '{opText}' is not allowlisted.")
        };

        object? value = null;
        if (el.TryGetProperty("value", out var valueEl))
        {
            value = valueEl.ValueKind switch
            {
                JsonValueKind.String => valueEl.GetString(),
                JsonValueKind.Number when valueEl.TryGetInt64(out var l) => l,
                JsonValueKind.Number when valueEl.TryGetDecimal(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => valueEl.EnumerateArray().Select(v =>
                    v.ValueKind == JsonValueKind.String ? (object?)v.GetString() :
                    v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n :
                    v.GetRawText()).ToList(),
                _ => valueEl.GetRawText()
            };
        }

        return new FilterComparison(field, op, value);
    }
}
