namespace Dayswork.Core.Machines;

/// <summary>
/// Narrows which items the worker may load into a group's machines: either <see cref="Any"/>
/// (load whatever the machine accepts) or an ordered preference list of allowed qualified ids.
/// The machine's own rules still decide ultimate acceptance; the filter only restricts the
/// candidate set further. Order is significant — earlier ids are preferred when supply is scarce.
/// </summary>
public sealed record MachineInputFilter
{
    public static MachineInputFilter Any { get; } = new(isAny: true, null);

    /// <summary>Allowed qualified ids in preference order. Empty when <see cref="IsAny"/> is true.</summary>
    public IReadOnlyList<string> AllowedQualifiedIds { get; }

    public bool IsAny { get; }

    private MachineInputFilter(bool isAny, IReadOnlyList<string>? allowedQualifiedIds)
    {
        IsAny = isAny;
        AllowedQualifiedIds = Normalize(allowedQualifiedIds);
    }

    /// <summary>
    /// Build a filter restricted to the given qualified ids (preference order preserved, blanks and
    /// duplicates dropped). An empty result collapses to <see cref="Any"/> so a group is never left
    /// with a filter that accepts nothing.
    /// </summary>
    public static MachineInputFilter Specific(IEnumerable<string>? allowedQualifiedIds)
    {
        var normalized = Normalize(allowedQualifiedIds is null ? null : allowedQualifiedIds.ToList());
        return normalized.Count == 0 ? Any : new MachineInputFilter(isAny: false, normalized);
    }

    public bool Allows(string qualifiedId) =>
        IsAny || AllowedQualifiedIds.Contains(qualifiedId, StringComparer.Ordinal);

    public bool Equals(MachineInputFilter? other) =>
        other is not null
        && IsAny == other.IsAny
        && AllowedQualifiedIds.SequenceEqual(other.AllowedQualifiedIds, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsAny);
        foreach (var id in AllowedQualifiedIds)
            hash.Add(id, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? ids)
    {
        if (ids is null)
            return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (seen.Add(id))
                ordered.Add(id);
        }

        return ordered.AsReadOnly();
    }
}
