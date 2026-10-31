using HotChocolate;
using Morph.Access.Domain;
using Morph.DataPlane.Query;
using Morph.DataPlane.Records;
using Morph.DataPlane.Services;
using Morph.Runtime.Api.Data;

namespace Morph.Runtime.Api.GraphQL;

/// <summary>
/// GraphQL-facing facade over <see cref="DataPlaneService"/> (same Access path as REST).
/// </summary>
public sealed class MorphDataGraphqlService
{
    private readonly DataPlaneService _data;

    public MorphDataGraphqlService(DataPlaneService data)
    {
        _data = data;
    }

    public async Task<MorphRecordGraphql?> GetRecordAsync(
        Guid environmentId,
        string entity,
        Guid id,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        var result = await _data.GetAsync(environmentId, entity, id, subject, cancellationToken)
            .ConfigureAwait(false);
        return MapOrThrow(result, entity);
    }

    public async Task<MorphRecordConnectionGraphql> ListRecordsAsync(
        Guid environmentId,
        string entity,
        string? filter,
        string? sort,
        int? pageSize,
        string? cursor,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        FilterNode? filterNode = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                filterNode = FilterJsonParser.Parse(filter);
            }
            catch (Exception ex)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage(ex.Message)
                        .SetCode("VALIDATION")
                        .Build());
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

        var result = await _data.ListAsync(
                environmentId,
                entity,
                new DynamicListRequest
                {
                    Filter = filterNode,
                    Sort = sorts,
                    PageSize = pageSize ?? 50,
                    Cursor = cursor
                },
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            ThrowAccess(result.Error!);
        }

        return new MorphRecordConnectionGraphql
        {
            Items = result.Value.Items.Select(i => ToGraphql(entity, i)).ToList(),
            NextCursor = result.Value.NextCursor,
            HasMore = result.Value.HasMore
        };
    }

    public async Task<MorphRecordGraphql> CreateRecordAsync(
        Guid environmentId,
        string entity,
        IReadOnlyDictionary<string, object?> fields,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        var result = await _data.CreateAsync(environmentId, entity, fields, subject, cancellationToken)
            .ConfigureAwait(false);
        return MapOrThrowRequired(result, entity);
    }

    public async Task<MorphRecordGraphql> UpdateRecordAsync(
        Guid environmentId,
        string entity,
        Guid id,
        IReadOnlyDictionary<string, object?> fields,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        var result = await _data.UpdateAsync(environmentId, entity, id, fields, subject, cancellationToken)
            .ConfigureAwait(false);
        return MapOrThrowRequired(result, entity);
    }

    public async Task<bool> DeleteRecordAsync(
        Guid environmentId,
        string entity,
        Guid id,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        var result = await _data.DeleteAsync(environmentId, entity, id, subject, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            ThrowAccess(result.Error!);
        }

        return true;
    }

    private static MorphRecordGraphql? MapOrThrow(
        Morph.BuildingBlocks.Results.Result<EntityRecordDocument> result,
        string entity)
    {
        if (result.IsFailure)
        {
            if (result.Error!.Code == "not_found")
            {
                return null;
            }

            ThrowAccess(result.Error);
        }

        return ToGraphql(entity, result.Value);
    }

    private static MorphRecordGraphql MapOrThrowRequired(
        Morph.BuildingBlocks.Results.Result<EntityRecordDocument> result,
        string entity)
    {
        if (result.IsFailure)
        {
            ThrowAccess(result.Error!);
        }

        return ToGraphql(entity, result.Value);
    }

    private static MorphRecordGraphql ToGraphql(string entity, EntityRecordDocument doc) => new()
    {
        Id = doc.Id,
        Entity = entity,
        DefinitionVersionId = doc.DefinitionVersionId,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt,
        CreatedBy = doc.CreatedBy,
        UpdatedBy = doc.UpdatedBy,
        RowVersion = doc.RowVersion,
        Fields = new Dictionary<string, object?>(doc.Fields, StringComparer.OrdinalIgnoreCase)
    };

    private static void ThrowAccess(Morph.BuildingBlocks.Results.Error error)
    {
        var code = error.Code switch
        {
            "forbidden" => "FORBIDDEN",
            "unauthorized" => "UNAUTHORIZED",
            "not_found" => "NOT_FOUND",
            "validation" => "VALIDATION",
            "conflict" => "CONFLICT",
            _ => "BAD_REQUEST"
        };

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(error.Message)
                .SetCode(code)
                .Build());
    }
}
