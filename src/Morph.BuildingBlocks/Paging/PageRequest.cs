namespace Morph.BuildingBlocks.Paging;

public sealed record PageRequest(int PageSize = 50, string? Cursor = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public int EffectivePageSize => Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
}
