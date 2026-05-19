using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal sealed class ContractDraft
{
    public HashSet<TaskKind> EnabledTasks { get; } = new();
    // Stubs — Zones filled in by U-11; Destinations filled in by U-11; Schedule selectable in U-12
    public List<Zone> Zones { get; } = new();
    public Dictionary<TaskKind, DestinationKey> Destinations { get; } = new();
    public ContractSchedule Schedule { get; set; } = ContractSchedule.OneTime;
}
