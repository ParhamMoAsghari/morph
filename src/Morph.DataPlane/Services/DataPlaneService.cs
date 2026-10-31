using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.BuildingBlocks.Paging;
using Morph.BuildingBlocks.Results;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Query;
using Morph.DataPlane.Records;

namespace Morph.DataPlane.Services;

public sealed class DataPlaneService
{
    private readonly IPublishedModelCache _cache;
    private readonly IDynamicRecordRepository _records;
    private readonly IAccessEvaluator _access;

    public DataPlaneService(
        IPublishedModelCache cache,
        IDynamicRecordRepository records,
        IAccessEvaluator access)
    {
        _cache = cache;
        _records = records;
        _access = access;
    }

    public async Task<Result<EntityRecordDocument>> CreateAsync(
        Guid environmentId,
        string entitySlug,
        IReadOnlyDictionary<string, object?> fields,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var model = await _cache.GetBySlugAsync(environmentId, entitySlug, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.NotFound($"Entity '{entitySlug}' is not published."));
        }

        var decision = await EvaluateAsync(model, AccessActions.Create, subject, resourceAttrs: null, cancellationToken)
            .ConfigureAwait(false);
        if (decision.IsDenied)
        {
            return Result.Failure<EntityRecordDocument>(Error.Forbidden(decision.Reason));
        }

        var sanitized = ApplyWriteMask(fields, decision.Obligations);
        var actorId = Guid.TryParse(subject.PrincipalId, out var g) ? g : (Guid?)null;
        var created = await _records.InsertAsync(model, sanitized, actorId, cancellationToken).ConfigureAwait(false);
        return Result.Success(ApplyReadMask(created, decision.Obligations));
    }

    public async Task<Result<EntityRecordDocument>> GetAsync(
        Guid environmentId,
        string entitySlug,
        Guid id,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var model = await _cache.GetBySlugAsync(environmentId, entitySlug, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.NotFound($"Entity '{entitySlug}' is not published."));
        }

        // First load without obligation to evaluate ABAC against resource attrs when needed.
        var raw = await _records.GetAsync(model, id, obligationFilter: null, cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.NotFound("Record was not found."));
        }

        var decision = await EvaluateAsync(
                model,
                AccessActions.Read,
                subject,
                ToResourceAttrs(raw),
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<EntityRecordDocument>(Error.Forbidden(decision.Reason));
        }

        var obligation = ToObligationFilter(decision.Obligations, subject);
        if (obligation is not null)
        {
            var gated = await _records.GetAsync(model, id, obligation, cancellationToken).ConfigureAwait(false);
            if (gated is null)
            {
                return Result.Failure<EntityRecordDocument>(Error.Forbidden("Record filtered by access obligation."));
            }

            raw = gated;
        }

        return Result.Success(ApplyReadMask(raw, decision.Obligations));
    }

    public async Task<Result<PageResult<EntityRecordDocument>>> ListAsync(
        Guid environmentId,
        string entitySlug,
        DynamicListRequest request,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var model = await _cache.GetBySlugAsync(environmentId, entitySlug, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure<PageResult<EntityRecordDocument>>(
                Error.NotFound($"Entity '{entitySlug}' is not published."));
        }

        var decision = await EvaluateAsync(model, AccessActions.List, subject, resourceAttrs: null, cancellationToken)
            .ConfigureAwait(false);
        if (decision.IsDenied)
        {
            return Result.Failure<PageResult<EntityRecordDocument>>(Error.Forbidden(decision.Reason));
        }

        var obligation = ToObligationFilter(decision.Obligations, subject);
        var page = await _records.ListAsync(model, request, obligation, cancellationToken).ConfigureAwait(false);
        var masked = page.Items.Select(i => ApplyReadMask(i, decision.Obligations)).ToList();
        return Result.Success(new PageResult<EntityRecordDocument>(masked, page.NextCursor, page.HasMore));
    }

    public async Task<Result<EntityRecordDocument>> UpdateAsync(
        Guid environmentId,
        string entitySlug,
        Guid id,
        IReadOnlyDictionary<string, object?> fields,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var model = await _cache.GetBySlugAsync(environmentId, entitySlug, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.NotFound($"Entity '{entitySlug}' is not published."));
        }

        var raw = await _records.GetAsync(model, id, obligationFilter: null, cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.NotFound("Record was not found."));
        }

        var decision = await EvaluateAsync(
                model,
                AccessActions.Update,
                subject,
                ToResourceAttrs(raw),
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure<EntityRecordDocument>(Error.Forbidden(decision.Reason));
        }

        var sanitized = ApplyWriteMask(fields, decision.Obligations);
        var actorId = Guid.TryParse(subject.PrincipalId, out var g) ? g : (Guid?)null;
        var obligation = ToObligationFilter(decision.Obligations, subject);
        var updated = await _records
            .UpdateAsync(model, id, sanitized, actorId, obligation, cancellationToken)
            .ConfigureAwait(false);

        if (updated is null)
        {
            return Result.Failure<EntityRecordDocument>(Error.Forbidden("Record filtered by access obligation."));
        }

        return Result.Success(ApplyReadMask(updated, decision.Obligations));
    }

    public async Task<Result> DeleteAsync(
        Guid environmentId,
        string entitySlug,
        Guid id,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var model = await _cache.GetBySlugAsync(environmentId, entitySlug, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result.Failure(Error.NotFound($"Entity '{entitySlug}' is not published."));
        }

        var raw = await _records.GetAsync(model, id, obligationFilter: null, cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return Result.Failure(Error.NotFound("Record was not found."));
        }

        var decision = await EvaluateAsync(
                model,
                AccessActions.Delete,
                subject,
                ToResourceAttrs(raw),
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            return Result.Failure(Error.Forbidden(decision.Reason));
        }

        var obligation = ToObligationFilter(decision.Obligations, subject);
        var deleted = await _records.DeleteAsync(model, id, obligation, cancellationToken).ConfigureAwait(false);
        return deleted
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Record filtered by access obligation."));
    }

    private async Task<AccessDecision> EvaluateAsync(
        PublishedEntityModel model,
        string action,
        AccessSubject subject,
        IReadOnlyDictionary<string, object?>? resourceAttrs,
        CancellationToken cancellationToken)
    {
        var context = new AccessContext
        {
            Subject = subject,
            Action = action,
            Resource = new AccessResource
            {
                Type = model.Slug,
                ProjectId = model.ProjectId,
                EnvironmentId = model.EnvironmentId,
                DefinitionId = model.DefinitionId,
                Attributes = resourceAttrs ?? new Dictionary<string, object?>()
            }
        };

        return await _access.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, object?> ToResourceAttrs(EntityRecordDocument record)
    {
        var map = new Dictionary<string, object?>(record.Fields, StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = record.Id,
            ["created_by"] = record.CreatedBy,
            ["owner_id"] = record.CreatedBy
        };
        return map;
    }

    internal static FilterNode? ToObligationFilter(AccessObligations obligations, AccessSubject subject)
    {
        if (obligations.RowFilters.Count == 0)
        {
            return null;
        }

        var nodes = new List<FilterNode>();
        foreach (var filter in obligations.RowFilters)
        {
            object? subjectValue = filter.SubjectAttribute.ToLowerInvariant() switch
            {
                "sub" or "id" or "principal_id" or "principalid" => subject.PrincipalId,
                _ => subject.Attributes.TryGetValue(filter.SubjectAttribute, out var v) ? v : subject.PrincipalId
            };

            if (subjectValue is string s && Guid.TryParse(s, out var g))
            {
                subjectValue = g;
            }

            nodes.Add(new FilterComparison(filter.ResourceAttribute, FilterOperator.Eq, subjectValue));
        }

        return nodes.Count == 1 ? nodes[0] : new FilterAnd(nodes);
    }

    internal static IReadOnlyDictionary<string, object?> ApplyWriteMask(
        IReadOnlyDictionary<string, object?> fields,
        AccessObligations obligations)
    {
        if (obligations.DeniedWriteFields.Count == 0)
        {
            return fields;
        }

        return fields
            .Where(kv => !obligations.DeniedWriteFields.Contains(kv.Key)
                         && !obligations.DeniedWriteFields.Any(d =>
                             kv.Key.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(kv.Key, d, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    internal static EntityRecordDocument ApplyReadMask(EntityRecordDocument record, AccessObligations obligations)
    {
        if (obligations.DeniedReadFields.Count == 0)
        {
            return record;
        }

        var fields = record.Fields
            .Where(kv => !IsDeniedField(kv.Key, obligations.DeniedReadFields))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        return new EntityRecordDocument
        {
            Id = record.Id,
            DefinitionVersionId = record.DefinitionVersionId,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            CreatedBy = record.CreatedBy,
            UpdatedBy = record.UpdatedBy,
            RowVersion = record.RowVersion,
            Fields = fields
        };
    }

    private static bool IsDeniedField(string key, IReadOnlySet<string> denied) =>
        denied.Contains(key)
        || denied.Any(d => key.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(key, d, StringComparison.OrdinalIgnoreCase)
                           || key.Equals(d, StringComparison.OrdinalIgnoreCase));
}
