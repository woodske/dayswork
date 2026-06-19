namespace Dayswork.Core.Machines;

/// <summary>
/// Per-group operating mode. No-input machines (bee houses, tappers, crystalariums,
/// <c>AllowLoadWhenFull</c>) are inherently collect-only and ignore this toggle.
/// </summary>
public enum MachineGroupMode
{
    /// <summary>Collect ready output, then reload empty machines from the group's input chest.</summary>
    CollectAndReload,

    /// <summary>Only collect ready output; never reload.</summary>
    CollectOnly,
}
