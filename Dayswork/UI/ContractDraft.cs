using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal sealed class ContractDraft
{
    public HashSet<TaskKind> EnabledTasks { get; } = new();
    public List<Zone> Zones { get; } = new();
    public Dictionary<TaskKind, DestinationKey> Destinations { get; } = new();
    public ContractSchedule Schedule { get; set; } = ContractSchedule.OneTime;
    // Non-null when editing an existing contract (U-12). ConfirmContract calls Update instead of Add.
    public ContractId? EditingId { get; set; }
}
