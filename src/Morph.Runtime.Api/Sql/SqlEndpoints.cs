using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Morph.DataPlane.Sql;
using Morph.Runtime.Api.Auth;

namespace Morph.Runtime.Api.Sql;

public static class SqlEndpoints
{
    public static IEndpointRouteBuilder MapSqlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/sql/v1")
            .WithTags("GovernedSql")
            .RequireAuthorization();

        group.MapPost("/query", ExecuteAsync);
        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext http,
        GovernedSqlService sql,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!RuntimeRequestContext.TryGetEnvironmentId(http, out var envId, out var envError))
        {
            return Results.BadRequest(new { detail = envError });
        }

        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("sql", out var sqlEl)
            || sqlEl.ValueKind != JsonValueKind.String)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sql"] = ["Request body must include a string 'sql' property."]
            });
        }

        var sqlText = sqlEl.GetString() ?? "";
        Dictionary<string, object?>? parameters = null;
        if (body.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
        {
            parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in paramsEl.EnumerateObject())
            {
                parameters[prop.Name] = JsonValue(prop.Value);
            }
        }

        var subject = RuntimeRequestContext.BuildSubject(http.User);
        var result = await sql.ExecuteAsync(envId, sqlText, parameters, subject, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ToHttpError(result.Error!);
        }

        var value = result.Value;
        return Results.Ok(new
        {
            kind = value.Kind.ToString().ToLowerInvariant(),
            entity = value.PrimaryEntity,
            rowCount = value.RowCount,
            truncated = value.Truncated,
            rows = value.Rows
        });
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

    private static IResult ToHttpError(Morph.BuildingBlocks.Results.Error error) => error.Code switch
    {
        "not_found" => Results.NotFound(new { detail = error.Message }),
        "forbidden" => Results.Json(new { detail = error.Message }, statusCode: StatusCodes.Status403Forbidden),
        "unauthorized" => Results.Json(new { detail = error.Message }, statusCode: StatusCodes.Status401Unauthorized),
        "validation" => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["sql"] = [error.Message]
        }),
        _ => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest)
    };
}
