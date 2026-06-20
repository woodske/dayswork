using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;

namespace Dayswork.Core.Shifts;

public sealed class ShiftContext
{
    public ContractId ContractId { get; }
    public WorkScopeSet WorkScopes { get; }
    public IReadOnlySet<TaskKind> EnabledTasks { get; }
    public IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations { get; }
    public ContractTermsSnapshot ContractTerms { get; }
    public ContractPreferences Preferences { get; }
    public WorkerEnergyState EnergyState { get; set; }
    public WorkerPacingProfile PacingProfile { get; }
    public ToolSnapshot ToolSnapshot { get; }
    public ShiftStateMachine StateMachine { get; } = new();
    public IReadOnlyList<WorkBatch> Batches { get; private set; }
    public int CurrentBatchIndex { get; set; }

    /// <summary>
    /// Swap in a fresh batch plan and rewind to its start. Used by the idle loop to re-run a
    /// machine-only pass after the first round of work is done; the original plan is spent by then.
    /// </summary>
    public void ResetBatches(IReadOnlyList<WorkBatch> batches)
    {
        Batches = batches ?? Array.Empty<WorkBatch>();
        CurrentBatchIndex = 0;
    }
    public Queue<WorkItem> WorkList { get; }
    public ItemBuffer Buffer { get; } = new();

    // Stardew HHMM time (e.g. 600 for 6am). Set at spawn.
    public int ShiftStartTime { get; }

    // Set when work list exhausts or 8pm fires — before Depositing begins.
    public int? ShiftEndTime { get; set; }

    // Counts how many teleport-and-resume recoveries have been attempted this shift.
    // Owned by the orchestrator layer; held here so it travels with the shift context.
    public int RecoveryAttempts { get; set; }

    // The single sink for everything undeliverable this shift:
    // seeded with automatic-overflow items at planning, appended on chest-full/missing and on
    // sleep-stop interruption, flushed through the shift outcome dispatcher at exit or sleep.
    public List<OverflowItem> Overflow { get; } = new();
    public ShiftStopReason? PendingStopReason { get; set; }

    public ShiftContext(
        ContractId contractId,
        WorkScopeSet workScopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyDictionary<TaskKind, DestinationKey> taskDestinations,
        ContractTermsSnapshot contractTerms,
        ContractPreferences preferences,
        WorkerEnergyState energyState,
        WorkerPacingProfile pacingProfile,
        ToolSnapshot toolSnapshot,
        IEnumerable<WorkItem> workList,
        int shiftStartTime,
        IReadOnlyList<WorkBatch>? batches = null,
        int currentBatchIndex = 0)
    {
        ContractId       = contractId;
        WorkScopes       = workScopes;
        EnabledTasks     = enabledTasks;
        TaskDestinations = taskDestinations;
        ContractTerms    = contractTerms;
        Preferences      = preferences;
        EnergyState      = energyState;
        PacingProfile    = pacingProfile;
        ToolSnapshot     = toolSnapshot;
        Batches          = batches ?? Array.Empty<WorkBatch>();
        CurrentBatchIndex = currentBatchIndex;
        WorkList         = new Queue<WorkItem>(workList);
        ShiftStartTime   = shiftStartTime;
    }
}
