using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

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
