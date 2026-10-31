namespace Morph.Access.Domain;

/// <summary>
/// Server-side obligations applied when a decision allows.
/// </summary>
public sealed class AccessObligations
{
    /// <summary>Field paths the principal must not read.</summary>
    public IReadOnlySet<string> DeniedReadFields { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Field paths the principal must not write.</summary>
    public IReadOnlySet<string> DeniedWriteFields { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Row-filter predicates forced into queries (e.g. owner_id = subject).
    /// Keys are resource attribute paths; values are subject attribute keys to bind.
    /// </summary>
    public IReadOnlyList<RowFilterObligation> RowFilters { get; init; } = [];

    public static AccessObligations Empty { get; } = new();

    public AccessObligations Merge(AccessObligations other)
    {
        var read = new HashSet<string>(DeniedReadFields, StringComparer.OrdinalIgnoreCase);
        foreach (var f in other.DeniedReadFields)
        {
            read.Add(f);
        }

        var write = new HashSet<string>(DeniedWriteFields, StringComparer.OrdinalIgnoreCase);
        foreach (var f in other.DeniedWriteFields)
        {
            write.Add(f);
        }

        var filters = RowFilters.Concat(other.RowFilters).ToList();
        return new AccessObligations
        {
            DeniedReadFields = read,
            DeniedWriteFields = write,
            RowFilters = filters
        };
    }
}

public sealed record RowFilterObligation(
    string ResourceAttribute,
    string SubjectAttribute,
    string Operator = "eq");
