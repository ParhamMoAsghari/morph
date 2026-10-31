namespace Morph.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
