using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Shifts;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

/// <summary>What to resume when a deposit run completes (see <see cref="ShiftSession.DepositResume"/>).</summary>
internal enum DepositResumeMode
{
    None,        // terminal deposit → wrap up and exit the farm
    ResumeIdle,  // pre-idle flush → re-enter the idle wait loop
    ResumeBatch, // eager mid-shift chest deposit → resume the next work batch
}

/// <summary>
/// All mutable state for one worker shift. Created by <see cref="ShiftOrchestrator.StartShift"/>
/// once spawn succeeds and discarded when the shift ends (exit, sleep, or session boundary) —
/// constructing a fresh session IS the per-shift reset. The orchestrator holds exactly one
/// nullable reference to it; everything here is non-existent outside a shift.
/// </summary>
internal sealed class ShiftSession
{
    public ShiftSession(
        ShiftContext ctx,
        FarmhandNpc worker,
        GameLocation currentLocation,
        TileCoord farmExitTile,
        TaskPriorityOrderer priorityOrderer,
        StuckDetector stuck)
    {
        Ctx = ctx;
        Worker = worker;
        CurrentLocation = currentLocation;
        FarmExitTile = farmExitTile;
        PriorityOrderer = priorityOrderer;
        Stuck = stuck;
    }

    /// <summary>The pure-Core shift context: state machine, energy, batches, buffer, overflow.</summary>
    public ShiftContext Ctx { get; }

    /// <summary>The NPC. Nullable because the worker despawns (sleep/exit) before the session ends.</summary>
    public FarmhandNpc? Worker;

    public GameLocation? CurrentLocation;
    public readonly TileCoord FarmExitTile;
    public readonly TaskPriorityOrderer PriorityOrderer;

    /// <summary>Replaced (with the post-teleport threshold) after the first stuck recovery.</summary>
    public StuckDetector Stuck;

    // ── Tick / pacing ────────────────────────────────────────────────────────
    public int TickCount;
    public int MorningEntranceHoldTicks;

    // ── Progress sampling / hit reaction ─────────────────────────────────────
    public int LastSampledGameTime;
    public Point LastTilePos;
    public bool PlayerWasSwinging;

    // ── Per-WorkItem action state (nav tile and task tile differ for trellis crops) ──
    public bool ActionPending;
    public TaskKind PendingTask;
    public TileCoord PendingNavTile;
    public TileCoord PendingTaskTile;
    public OutputScopeProvenance PendingOutputProvenance = OutputScopeProvenance.Unknown;
    public LaborBeatOutcome? PendingBeatOutcome;

    // ── Active-batch work queues ─────────────────────────────────────────────
    public readonly Queue<AnimalWorkItem> AnimalWork = new();
    public readonly List<WorkItem> DeferredTileWork = new();
    public readonly List<AnimalWorkItem> DeferredAnimalWork = new();
    public WorkItem? CurrentTileWork;
    public AnimalWorkItem? CurrentAnimalWork;
    public int BatchSelectionAttempts;
    public int MaxBatchSelectionAttempts = 4;

    // ── Feed work ────────────────────────────────────────────────────────────
    public FeedWorkPlan? CurrentFeedPlan;
    public int HayInHand;

    // ── FarmForage rescan guard (each tile re-enqueued at most once per batch) ──
    public int RescanBatchIndex = -1;
    public readonly HashSet<TileCoord> RescanEnqueuedTiles = new();

    // ── Debris sweeps / deposit gating / exit ────────────────────────────────
    public readonly List<PendingDebrisSweep> PendingDebrisSweeps = new();
    public bool WaitingForDebrisBeforeDeposit;
    public TileCoord CurrentExitTile;

    // ── Dev-only worker-action leak audit (gated by DevLog.Enabled) ───────────
    // Counts worker-created item-debris that vanilla mis-routed into the player's location instead
    // of the work location (see InvokeTaskActionGuarded + docs/debris-and-drops.md). Recovered =
    // caught by the guard's foreign sweep; stranded = survived recovery — the real tripwire, which
    // should stay 0. Surfaced via dayswork_debug_leaks and the shift-end summary.
    public int LeakBeatsObserved;
    public int LeakItemsRecovered;
    public int LeakItemsStranded;

    // ── Cross-location travel ────────────────────────────────────────────────
    public TravelPurpose TravelPurpose;

    // ── Managed shopping / deposit trips ─────────────────────────────────────
    // Assigned by StartShift immediately after the session is constructed (both need the
    // session reference, so they can't be constructor parameters here).
    public ManagedShoppingCoordinator Shopping = null!;
    public DepositTripRunner Deposits = null!;

    // ── Managed-crop batch state ─────────────────────────────────────────────
    public readonly Queue<TileAction> ManagedActions = new();
    public TileAction? CurrentManagedAction;
    public bool ManagedActive;
    public List<CropZoneAssignment> ManagedAssignments = new();
    public int ManagedReplanCount;
    public string LastManagedSignature = string.Empty;
    public string ManagedBatchLocationName = "Farm";

    // One consolidated managed-crop shopping trip per shift covers every location's needs up front.
    // Set true when that trip is resolved (started, or found nothing to buy) so it never re-fires as
    // later managed batches begin; a fresh session per shift is the reset. Not touched by the
    // per-batch ResetManagedCropState.
    public bool ManagedShoppingResolved;

    // ── Idle loop (post-work "repeat a task until cap/exhaustion") ────────────
    // True only while parked at the office door polling for ready machines (no travel, no action).
    public bool IdleWaiting;
    // What the worker should do when the in-flight deposit run finishes: nothing special (terminal
    // wrap-up → exit), re-enter the idle wait loop (pre-idle flush), or resume the next work batch
    // (eager mid-shift chest deposit). Only one is ever in effect at a time.
    public DepositResumeMode DepositResume = DepositResumeMode.None;
    // Throttled-tick counter while parked, drives the periodic music-note re-emote.
    public int IdleWaitTicks;
    // Direction the worker faces while waiting (away from the office door); re-applied each emote.
    public int IdleWaitFacing = 2;

    // ── Machine batch state ──────────────────────────────────────────────────
    public readonly Queue<MachineStep> MachineSteps = new();
    public MachineStep? CurrentMachineStep;
    public bool MachinesActive;
    public string MachineBatchLocationName = "Farm";

    // Groups in the current batch still to be serviced, FIFO. Drained one at a time so each group
    // runs a full collect→reload cycle before the next (per-group, not collect-all-then-reload-all).
    public readonly Queue<MachineGroup> PendingMachineGroups = new();

    // Pending reload jobs for the current batch (one per group with empty machines to load). Each is
    // a fetch-then-load: withdraw the planned inputs from the group's chest, then load its machines.
    public readonly Queue<MachineReloadJob> MachineReloads = new();
    public MachineReloadJob? CurrentMachineReload;

    // True while the worker is walking to the current reload job's input chest to withdraw inputs.
    public bool MachineFetchPending;

    // Physical input carry buffer for the reload fetch: qualified id → the real withdrawn item
    // stacks, plus the chest they came from (so leftovers settle back there on stop). The real items
    // are carried — not a bare count — so flavored inputs (Sturgeon/flavored roe, etc.) keep their
    // identity end-to-end and the machine produces the correct output; nothing is lost or degraded
    // (hard rule 4).
    public readonly Dictionary<string, List<Item>> CarriedInputs = new(StringComparer.Ordinal);
    public ChestRef? CarriedInputsChest;

    // Capture-and-clone store for flavored/colored output (roe, wine, jelly, flavored honey…) so the
    // deposit pipeline rebuilds the exact item instead of a generic one. Fresh per shift.
    public readonly FlavorItemRegistry Flavors = new();

    // ── Fish pond batch state ────────────────────────────────────────────────
    // Collect-only: each step visits one ready pond, takes its output into the deposit buffer. No
    // reload/fetch machinery (the player stocks the fish).
    public readonly Queue<FishPondStep> FishPondSteps = new();
    public FishPondStep? CurrentFishPondStep;
    public bool FishPondsActive;
    public string FishPondBatchLocationName = "Farm";
}

/// <summary>A delayed debris collection pass (felled-tree trunks, shaken fruit settle late).</summary>
internal sealed class PendingDebrisSweep
{
    public PendingDebrisSweep(
        GameLocation location,
        Vector2 origin,
        HashSet<Debris> baseline,
        int ticksRemaining,
        int radiusTiles,
        TaskKind sourceTask,
        OutputScopeProvenance provenance)
    {
        Location = location;
        Origin = origin;
        Baseline = baseline;
        TicksRemaining = ticksRemaining;
        RadiusTiles = radiusTiles;
        SourceTask = sourceTask;
        Provenance = provenance;
    }

    public GameLocation Location { get; }
    public Vector2 Origin { get; }
    public HashSet<Debris> Baseline { get; }
    public int TicksRemaining { get; set; }
    public int RadiusTiles { get; }
    public TaskKind SourceTask { get; }
    public OutputScopeProvenance Provenance { get; }
}

internal sealed record LaborBeatOutcome(bool UnitResolved, bool TaskFullyComplete);
