namespace Morph.EntityEngine.Domain;

/// <summary>
/// Authoring IR field. <see cref="FieldId"/> is immutable identity; slug/display may change.
/// </summary>
public sealed class FieldDefinition
{
    public required Guid FieldId { get; init; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required FieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    public bool IsIndexed { get; set; }

    public bool IsUnique { get; set; }

    /// <summary>Schema hook for Morph.Crypto field encryption (Phase 10+).</summary>
    public bool Encrypt { get; set; }

    public FieldDefinition Clone() => new()
    {
        FieldId = FieldId,
        Slug = Slug,
        DisplayName = DisplayName,
        DataType = DataType,
        IsRequired = IsRequired,
        IsIndexed = IsIndexed,
        IsUnique = IsUnique,
        Encrypt = Encrypt
    };
}
