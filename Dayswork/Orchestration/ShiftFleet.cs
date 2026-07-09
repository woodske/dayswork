using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Dayswork.Orchestration;

/// <summary>
/// Shared state for one game day across all concurrent shifts. Created fresh each DayStarted —
/// discarding the previous instance is the reset (mirroring the ShiftSession lifecycle).
/// </summary>
internal sealed class FleetDay
{
    /// <summary>Work claims shared by every concurrent shift so overlapping contract scopes
    /// never double-service a work item.</summary>
    public WorkClaimRegistry Claims { get; } = new();

    /// <summary>Gold each concurrent shift has committed to a managed-crop shopping trip, so a
    /// second farmhand doesn't also travel to buy seeds the first is already on its way to buy.</summary>
    public ShoppingBudgetLedger ShoppingBudget { get; } = new();

    /// <summary>Spawn tiles already taken this morning, so each worker appears on its own tile
    /// near the office door. Reserved only when a worker actually spawns.</summary>
    public HashSet<TileCoord> ReservedSpawnTiles { get; } = new();

    private int _spawnCount;

    /// <summary>0-based index of the next worker to spawn today; drives the morning entrance
    /// hold stagger so workers file out one at a time.</summary>
    public int NextSpawnIndex() => _spawnCount++;

    /// <summary>True once any shift has finished its normal deposit-and-exit today. The office
    /// evening overlay lights when this is set AND no shifts remain live (everyone's done).</summary>
    public bool AnyNormalExit { get; private set; }

    public void ReportNormalExit() => AnyNormalExit = true;
}

/// <summary>
/// Runs one <see cref="ShiftOrchestrator"/> per active contract so multiple farmhands can work the
/// same day. Subscribes once to the game events and fans out to every live orchestrator
/// <b>sequentially</b> — that sequencing is the concurrency model: each worker's guarded
/// vanilla-API beat runs synchronously inside one event callback, so two workers can never
/// interleave their Game1.player snapshot/restore. Orchestrators whose session ends are pruned;
/// a fresh orchestrator (with its own movement driver, tool animator, and travel runner) is
/// created per shift.
/// </summary>
internal sealed class ShiftFleet : ISessionBoundaryResettable
{
    private readonly Func<ShiftOrchestrator> _createOrchestrator;
    private readonly List<ShiftOrchestrator> _active = new();
    private FleetDay _day = new();

    public ShiftFleet(Func<ShiftOrchestrator> createOrchestrator) =>
        _createOrchestrator = createOrchestrator;

    public int ActiveCount => _active.Count;

    public bool IsShiftRunning(ContractId contractId) =>
        _active.Any(o => o.ActiveContractId == contractId);

    public void StartShift(Contract contract, ConfigSnapshot runtimeConfig)
    {
        if (IsShiftRunning(contract.Id))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] StartShift called for contract {contract.Id.Value} which already has a running shift — ignoring.",
                DevLog.WarnLevel);
            return;
        }

        var orchestrator = _createOrchestrator();
        orchestrator.StartShift(contract, runtimeConfig, _day);

        // The no-applicable-work guard may decline to spawn; only track orchestrators that
        // actually opened a session.
        if (orchestrator.ActiveContractId is not null)
            _active.Add(orchestrator);
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _day = new FleetDay();
        // Office goes dark again each morning; re-lit when the last worker finishes for the day.
        HiringBuilding.WorkCompletedToday = false;
        CropHudNotifier.ResetForDay();
    }

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnUpdateTicked(sender, e);
        PruneFinished();
    }

    public void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnTimeChanged(sender, e);
    }

    public void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnObjectListChanged(sender, e);
    }

    public void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnTerrainFeatureListChanged(sender, e);
    }

    public void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnFurnitureListChanged(sender, e);
    }

    public void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.OnBuildingListChanged(sender, e);
    }

    /// <summary>Sleep/save settle: every live shift stops, settles items, and despawns its worker
    /// (hard rules 4 and 5 apply per worker).</summary>
    public void StopForSleepAndSettle()
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.StopForSleepAndSettle();
        _active.Clear();
    }

    public void ResetForSessionBoundary(SessionResetBoundary boundary)
    {
        foreach (var orchestrator in Snapshot())
            orchestrator.ResetForSessionBoundary(boundary);
        _active.Clear();
        _day = new FleetDay();
    }

    /// <summary>Ends shifts early (dev console). No filter ends every live shift; a filter ends
    /// shifts whose contract id starts with it (dashes ignored, case-insensitive).</summary>
    public void EndShiftEarly(string? contractIdPrefix = null)
    {
        var any = false;
        foreach (var orchestrator in Snapshot())
        {
            if (contractIdPrefix is not null && !MatchesContract(orchestrator.ActiveContractId, contractIdPrefix))
                continue;
            any = true;
            orchestrator.EndShiftEarly();
        }

        if (!any)
            ModEntry.ModMonitor.Log("[Dayswork] No active shift matched to cancel.", LogLevel.Trace);
        // The ended shifts route through deposit → exit over the next ticks; pruning happens there.
    }

    public void LogLeakAudit(LogLevel level)
    {
        if (_active.Count == 0)
        {
            ModEntry.ModMonitor.Log("[Dayswork][leak-detector] No active shift.", level);
            return;
        }

        foreach (var orchestrator in Snapshot())
            orchestrator.LogLeakAudit(level);
    }

    // Fan-out iterates a snapshot so an orchestrator ending its own session mid-callback can't
    // invalidate the loop.
    private ShiftOrchestrator[] Snapshot() => _active.ToArray();

    private void PruneFinished()
    {
        if (_active.RemoveAll(o => o.ActiveContractId is null) == 0)
            return;

        // Evening office lights/smoke: only once the LAST worker has left, and only when at least
        // one shift finished normally (sleep-settle and session resets don't count, same as before).
        if (_active.Count == 0 && _day.AnyNormalExit)
            HiringBuilding.WorkCompletedToday = true;
    }

    private static bool MatchesContract(ContractId? id, string prefix)
    {
        if (id is null)
            return false;

        var normalized = prefix.Replace("-", "");
        return id.Value.Value.ToString("N").StartsWith(normalized, StringComparison.OrdinalIgnoreCase);
    }
}
