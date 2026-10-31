using Morph.BuildingBlocks.Paging;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Query;

namespace Morph.DataPlane.Records;

public interface IDynamicRecordRepository
{
    Task<EntityRecordDocument> InsertAsync(
        PublishedEntityModel model,
        IReadOnlyDictionary<string, object?> fields,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    Task<EntityRecordDocument?> GetAsync(
        PublishedEntityModel model,
        Guid id,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default);

    Task<PageResult<EntityRecordDocument>> ListAsync(
        PublishedEntityModel model,
        DynamicListRequest request,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default);

    Task<EntityRecordDocument?> UpdateAsync(
        PublishedEntityModel model,
        Guid id,
        IReadOnlyDictionary<string, object?> fields,
        Guid? actorId,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        PublishedEntityModel model,
        Guid id,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default);
}
