namespace Dayswork.Core.Machines;

/// <summary>
/// The persisted, authored "Manage Machines" plan: an ordered set of <see cref="MachineGroup"/>s.
/// Carried directly on the contract. Empty groups are dropped on construction.
/// </summary>
public sealed record MachineWorkScope
{
    public static MachineWorkScope Empty { get; } = new(Array.Empty<MachineGroup>());

    public IReadOnlyList<MachineGroup> Groups { get; }

    public MachineWorkScope(IReadOnlyList<MachineGroup>? groups)
    {
        Groups = (groups ?? Array.Empty<MachineGroup>())
            .Where(group => group.IsEnabled)
            .OrderBy(group => group.Id, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public bool IsEnabled => Groups.Count > 0;

    /// <summary>Every machine across all groups (flattened).</summary>
    public IEnumerable<MachineRef> AllMachines => Groups.SelectMany(group => group.Machines);

    public bool Equals(MachineWorkScope? other) =>
        other is not null && Groups.SequenceEqual(other.Groups);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var group in Groups)
            hash.Add(group);
        return hash.ToHashCode();
    }
}
