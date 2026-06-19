using Dayswork.Core.Domain;
using Dayswork.Core.Machines;

namespace Dayswork.UI;

/// <summary>
/// A single machine group being authored: a mode, an input filter, an input chest, an output
/// destination, and the machines selected on the map. Projects into one persisted
/// <see cref="MachineGroup"/>. Transient and mutable.
/// </summary>
internal sealed class MachineGroupDraft
{
    public MachineGroupDraft(string id)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException(null, nameof(id)) : id;
    }

    public string Id { get; }

    /// <summary>The machine type (qualified id) this group services. Null until one is chosen.</summary>
    public string? MachineType { get; private set; }
    public MachineGroupMode Mode { get; set; } = MachineGroupMode.CollectAndReload;
    public ChestRef? InputChest { get; set; }
    public DestinationKey? OutputDestination { get; set; }

    public bool HasType => !string.IsNullOrWhiteSpace(MachineType);

    /// <summary>Allowed input ids in preference order. Empty ⇒ "Any input".</summary>
    public List<string> AllowedInputIds { get; } = new();

    public List<MachineRef> Machines { get; } = new();

    public bool IsAnyInput => AllowedInputIds.Count == 0;
    public bool HasAnyMachine => Machines.Count > 0;

    public bool RequiresInputChest => Mode == MachineGroupMode.CollectAndReload && HasAnyMachine && InputChest is null;

    /// <summary>
    /// Set the group's machine type. Machine selections and the input filter are type-scoped, so
    /// changing the type clears both (the previously selected machines are a different type and the
    /// accepted-input list no longer applies).
    /// </summary>
    public void SetMachineType(string? machineType)
    {
        var normalized = string.IsNullOrWhiteSpace(machineType) ? null : machineType;
        if (string.Equals(normalized, MachineType, StringComparison.Ordinal))
            return;

        MachineType = normalized;
        Machines.Clear();
        AllowedInputIds.Clear();
    }

    public void ToggleAllowedInput(string qualifiedId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedId))
            return;

        if (!AllowedInputIds.Remove(qualifiedId))
            AllowedInputIds.Add(qualifiedId);
    }

    public void ClearInputFilter() => AllowedInputIds.Clear();

    public MachineInputFilter BuildFilter() =>
        IsAnyInput ? MachineInputFilter.Any : MachineInputFilter.Specific(AllowedInputIds);

    public MachineGroup BuildGroup() =>
        new(
            Id,
            Machines.ToList(),
            BuildFilter(),
            InputChest,
            OutputDestination ?? AutomaticOutputDestination.Instance,
            Mode,
            MachineType);

    internal void HydrateFrom(MachineGroup group)
    {
        MachineType = group.MachineType;
        Mode = group.Mode;
        InputChest = group.InputChest;
        OutputDestination = group.OutputDestination;
        AllowedInputIds.Clear();
        if (!group.InputFilter.IsAny)
            AllowedInputIds.AddRange(group.InputFilter.AllowedQualifiedIds);
        Machines.Clear();
        Machines.AddRange(group.Machines);
    }
}

/// <summary>
/// Transient, mutable in-progress machine plan the Manage Machines pages edit. Owns multiple machine
/// groups, then projects them into the persisted <see cref="MachineWorkScope"/>.
/// </summary>
internal sealed class MachinePlanDraft
{
    private readonly List<MachineGroupDraft> _groups = new();
    private int _nextGroupNumber = 1;

    public IReadOnlyList<MachineGroupDraft> Groups => _groups;

    public bool HasAnyAssignment => _groups.Any(group => group.HasAnyMachine);

    public MachineGroupDraft AddGroup()
    {
        var group = new MachineGroupDraft(NextGroupId());
        _groups.Add(group);
        return group;
    }

    public MachineGroupDraft GetGroup(string groupId) =>
        _groups.First(group => string.Equals(group.Id, groupId, StringComparison.Ordinal));

    public bool TryGetGroup(string groupId, out MachineGroupDraft group)
    {
        group = _groups.FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.Ordinal))!;
        return group is not null;
    }

    public void DeleteGroup(string groupId) =>
        _groups.RemoveAll(group => string.Equals(group.Id, groupId, StringComparison.Ordinal));

    public void SetGroupMachines(string groupId, IEnumerable<MachineRef> machines)
    {
        if (!TryGetGroup(groupId, out var group))
            return;

        group.Machines.Clear();
        group.Machines.AddRange(machines);
    }

    /// <summary>
    /// Machines claimed by groups OTHER than <paramref name="activeGroupId"/> — rendered protected on
    /// the map so one machine belongs to exactly one group.
    /// </summary>
    public IReadOnlyList<MachineRef> ProtectedMachines(string activeGroupId) =>
        _groups
            .Where(group => !string.Equals(group.Id, activeGroupId, StringComparison.Ordinal))
            .SelectMany(group => group.Machines)
            .ToList()
            .AsReadOnly();

    public MachineWorkScope BuildScope() =>
        new(_groups.Select(group => group.BuildGroup()).ToList());

    public void HydrateFrom(MachineWorkScope scope)
    {
        _groups.Clear();
        _nextGroupNumber = 1;

        if (!scope.IsEnabled)
            return;

        foreach (var group in scope.Groups)
        {
            var draft = new MachineGroupDraft(string.IsNullOrWhiteSpace(group.Id) ? NextGroupId() : group.Id);
            draft.HydrateFrom(group);
            _groups.Add(draft);
        }
    }

    private string NextGroupId()
    {
        string id;
        do
        {
            id = $"machine-group-{_nextGroupNumber++}";
        }
        while (_groups.Any(group => string.Equals(group.Id, id, StringComparison.Ordinal)));

        return id;
    }
}
