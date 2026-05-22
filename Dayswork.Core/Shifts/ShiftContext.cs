using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Core.Shifts;

public sealed class ShiftContext
{
    public ContractId ContractId { get; }
    public IReadOnlyList<Zone> Zones { get; }
    public IReadOnlySet<TaskKind> EnabledTasks { get; }
    public IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations { get; }
    public int DepositAmount { get; }
    public int HourlyRate { get; }
    public ToolSnapshot ToolSnapshot { get; }
    public ShiftStateMachine StateMachine { get; } = new();
    public Queue<WorkItem> WorkList { get; }
    public ItemBuffer Buffer { get; } = new();

    // Stardew HHMM time (e.g. 600 for 6am). Set at spawn.
    public int ShiftStartTime { get; }

    // Set when work list exhausts or 8pm fires — before Depositing begins.
    public int? ShiftEndTime { get; set; }

    // Counts how many teleport-and-resume recoveries have been attempted this shift (Pattern E).
    // Owned by the orchestrator layer; held here so it travels with the shift context.
    public int RecoveryAttempts { get; set; }

    // The single sink for everything undeliverable this shift (Pattern O / FD-Q5/Q6=A):
    // seeded with mail-bound items at planning, appended on chest-full/missing and on
    // sleep-stop interruption, flushed to one settlement letter at exit or sleep.
    public List<OverflowItem> Overflow { get; } = new();

    public ShiftContext(
        ContractId contractId,
        IReadOnlyList<Zone> zones,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyDictionary<TaskKind, DestinationKey> taskDestinations,
        int depositAmount,
        int hourlyRate,
        ToolSnapshot toolSnapshot,
        IEnumerable<WorkItem> workList,
        int shiftStartTime)
    {
        ContractId       = contractId;
        Zones            = zones;
        EnabledTasks     = enabledTasks;
        TaskDestinations = taskDestinations;
        DepositAmount    = depositAmount;
        HourlyRate       = hourlyRate;
        ToolSnapshot     = toolSnapshot;
        WorkList         = new Queue<WorkItem>(workList);
        ShiftStartTime   = shiftStartTime;
    }

    public int ComputeRefund()
    {
        var endTime      = ShiftEndTime ?? ShiftStartTime;
        var workedMins   = Math.Max(0, HhmmToMinutes(endTime) - HhmmToMinutes(ShiftStartTime));
        var hoursWorked  = workedMins / 60;
        var billed       = hoursWorked * HourlyRate;
        return Math.Clamp(DepositAmount - billed, 0, DepositAmount);
    }

    private static int HhmmToMinutes(int hhmm)
    {
        var hours   = hhmm / 100;
        var minutes = hhmm % 100;
        return hours * 60 + minutes;
    }
}
