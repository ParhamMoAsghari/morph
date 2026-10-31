namespace Morph.BuildingBlocks.Paging;

public sealed record PageResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
