using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Core.Shifts;

public sealed class ShiftContext
{
    public ContractId ContractId { get; }
    public IReadOnlyList<Zone> Zones { get; }
    public IReadOnlySet<TaskKind> EnabledTasks { get; }
    public int DepositAmount { get; }
    public int HourlyRate { get; }
    public ToolSnapshot ToolSnapshot { get; }
    public ShiftStateMachine StateMachine { get; } = new();
    public Queue<WorkItem> WorkList { get; }
    public ItemBuffer Buffer { get; } = new();

    // Game-minutes from midnight. Always 360 (6am). Set at spawn.
    public int ShiftStartTime { get; }

    // Set when work list exhausts or 8pm fires — before Depositing begins.
    public int? ShiftEndTime { get; set; }

    // Counts how many teleport-and-resume recoveries have been attempted this shift (Pattern E).
    // Owned by the orchestrator layer; held here so it travels with the shift context.
    public int RecoveryAttempts { get; set; }

    // Task kinds skipped entirely because the player's tool level was too low (BR-TOOL-02).
    // Populated during BuildWorkList; read by U-14 to send the warning mail.
    public HashSet<TaskKind> ToolMissingWarnings { get; } = new();

    public ShiftContext(
        ContractId contractId,
        IReadOnlyList<Zone> zones,
        IReadOnlySet<TaskKind> enabledTasks,
        int depositAmount,
        int hourlyRate,
        ToolSnapshot toolSnapshot,
        IEnumerable<WorkItem> workList,
        int shiftStartTime)
    {
        ContractId    = contractId;
        Zones         = zones;
        EnabledTasks  = enabledTasks;
        DepositAmount = depositAmount;
        HourlyRate    = hourlyRate;
        ToolSnapshot  = toolSnapshot;
        WorkList      = new Queue<WorkItem>(workList);
        ShiftStartTime = shiftStartTime;
    }

    public int ComputeRefund()
    {
        var endTime      = ShiftEndTime ?? ShiftStartTime;
        var hoursWorked  = (endTime - ShiftStartTime) / 60;
        var billed       = hoursWorked * HourlyRate;
        return Math.Clamp(DepositAmount - billed, 0, DepositAmount);
    }
}
