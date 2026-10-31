namespace Morph.EntityEngine.Domain;

/// <summary>
/// Explicit diamond / path collision rule. Silent merges of distinct FieldIds are denied.
/// </summary>
public sealed class ConflictResolution
{
    /// <summary>
    /// Effective path that collides (e.g. "email" or "person.email").
    /// Compared case-insensitively after normalization.
    /// </summary>
    public required string ConflictPath { get; init; }

    /// <summary>Winning field identity when multiple distinct FieldIds claim the path.</summary>
    public required Guid WinningFieldId { get; init; }

    public ConflictResolution Clone() => new()
    {
        ConflictPath = ConflictPath,
        WinningFieldId = WinningFieldId
    };
}
