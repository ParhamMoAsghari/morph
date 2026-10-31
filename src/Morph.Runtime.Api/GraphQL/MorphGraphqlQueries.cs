using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using Morph.Access.Domain;
using Morph.Runtime.Api.Auth;

namespace Morph.Runtime.Api.GraphQL;

[Authorize]
public sealed class MorphGraphqlQueries
{
    /// <summary>Fetch a single published entity record by id.</summary>
    public Task<MorphRecordGraphql?> RecordAsync(
        string entity,
        Guid id,
        [Service] MorphDataGraphqlService data,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var (envId, subject) = GraphqlRequest.Require(httpContextAccessor, claimsPrincipal);
        return data.GetRecordAsync(envId, entity, id, subject, cancellationToken);
    }

    /// <summary>
    /// List published entity records. <paramref name="filter"/> uses the same JSON AST as REST
    /// (<c>{"field":"title","op":"eq","value":"x"}</c> / and/or/not).
    /// </summary>
    public Task<MorphRecordConnectionGraphql> RecordsAsync(
        string entity,
        string? filter,
        string? sort,
        int? pageSize,
        string? cursor,
        [Service] MorphDataGraphqlService data,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var (envId, subject) = GraphqlRequest.Require(httpContextAccessor, claimsPrincipal);
        return data.ListRecordsAsync(envId, entity, filter, sort, pageSize, cursor, subject, cancellationToken);
    }
}

[Authorize]
public sealed class MorphGraphqlMutations
{
    public Task<MorphRecordGraphql> CreateRecordAsync(
        string entity,
        [GraphQLType(typeof(AnyType))] Dictionary<string, object?> fields,
        [Service] MorphDataGraphqlService data,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var (envId, subject) = GraphqlRequest.Require(httpContextAccessor, claimsPrincipal);
        return data.CreateRecordAsync(envId, entity, fields, subject, cancellationToken);
    }

    public Task<MorphRecordGraphql> UpdateRecordAsync(
        string entity,
        Guid id,
        [GraphQLType(typeof(AnyType))] Dictionary<string, object?> fields,
        [Service] MorphDataGraphqlService data,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var (envId, subject) = GraphqlRequest.Require(httpContextAccessor, claimsPrincipal);
        return data.UpdateRecordAsync(envId, entity, id, fields, subject, cancellationToken);
    }

    public Task<bool> DeleteRecordAsync(
        string entity,
        Guid id,
        [Service] MorphDataGraphqlService data,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var (envId, subject) = GraphqlRequest.Require(httpContextAccessor, claimsPrincipal);
        return data.DeleteRecordAsync(envId, entity, id, subject, cancellationToken);
    }
}

internal static class GraphqlRequest
{
    public static (Guid EnvId, AccessSubject Subject) Require(
        IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal)
    {
        var http = httpContextAccessor.HttpContext
                   ?? throw new GraphQLException(
                       ErrorBuilder.New()
                           .SetMessage("HTTP context is required.")
                           .SetCode("BAD_REQUEST")
                           .Build());

        if (!RuntimeRequestContext.TryGetEnvironmentId(http, out var envId, out var error))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(error ?? "Missing environment id.")
                    .SetCode("BAD_REQUEST")
                    .Build());
        }

        return (envId, RuntimeRequestContext.BuildSubject(claimsPrincipal));
    }
}
