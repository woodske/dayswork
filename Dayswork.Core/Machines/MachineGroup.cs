namespace Dayswork.Core.Machines;

using Dayswork.Core.Domain;

/// <summary>
/// A bundle of <c>{selected machines, input filter, input chest, output destination, mode}</c>.
/// A machine belongs to exactly one group; a group may span multiple locations. At plan time
/// machines are bucketed by location into batches, and each machine's config is looked up from its
/// owning group.
/// </summary>
public sealed record MachineGroup
{
    public string Id { get; }

    /// <summary>
    /// The single machine type (qualified id, e.g. <c>(BC)16</c>) this group services. A group is
    /// homogeneous — its machines, accepted inputs, and required companions are all derived from this
    /// type. Null on a freshly created group before a type is chosen.
    /// </summary>
    public string? MachineType { get; }
    public IReadOnlyList<MachineRef> Machines { get; }
    public MachineInputFilter InputFilter { get; }
    public ChestRef? InputChest { get; }
    public DestinationKey OutputDestination { get; }
    public MachineGroupMode Mode { get; }

    public MachineGroup(
        string id,
        IReadOnlyList<MachineRef>? machines,
        MachineInputFilter? inputFilter = null,
        ChestRef? inputChest = null,
        DestinationKey? outputDestination = null,
        MachineGroupMode mode = MachineGroupMode.CollectAndReload,
        string? machineType = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Machine group id must be set.", nameof(id)) : id;
        MachineType = string.IsNullOrWhiteSpace(machineType) ? null : machineType;
        Machines = (machines ?? Array.Empty<MachineRef>())
            .Where(machine => machine is not null && !string.IsNullOrWhiteSpace(machine.LocationName))
            .Distinct()
            .OrderBy(machine => machine.LocationName, StringComparer.Ordinal)
            .ThenBy(machine => machine.Tile.Y)
            .ThenBy(machine => machine.Tile.X)
            .ToList()
            .AsReadOnly();
        InputFilter = inputFilter ?? MachineInputFilter.Any;
        InputChest = inputChest;
        OutputDestination = outputDestination ?? AutomaticOutputDestination.Instance;
        Mode = mode;
    }

    public bool IsEnabled => Machines.Count > 0;

    /// <summary>True when this group ever needs an input chest (reload mode with at least one machine).</summary>
    public bool RequiresInput => Mode == MachineGroupMode.CollectAndReload && Machines.Count > 0;

    public bool Equals(MachineGroup? other) =>
        other is not null
        && string.Equals(Id, other.Id, StringComparison.Ordinal)
        && string.Equals(MachineType, other.MachineType, StringComparison.Ordinal)
        && Machines.SequenceEqual(other.Machines)
        && InputFilter.Equals(other.InputFilter)
        && Equals(InputChest, other.InputChest)
        && OutputDestination.Equals(other.OutputDestination)
        && Mode == other.Mode;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(MachineType, StringComparer.Ordinal);
        hash.Add(Machines.Count);
        hash.Add(InputFilter);
        hash.Add(InputChest);
        hash.Add(OutputDestination);
        hash.Add(Mode);
        return hash.ToHashCode();
    }
}
