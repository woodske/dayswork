using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Dayswork.Orchestration;

/// <summary>
/// Shared state for one game day across every concurrent shift. Created fresh each DayStarted —
/// discarding the previous instance is the reset (mirroring the ShiftSession lifecycle).
/// </summary>
internal sealed class FleetDay
{
    /// <summary>Work claims shared by every concurrent shift so overlapping contract scopes
    /// never double-service a work item.</summary>
    public WorkClaimRegistry Claims { get; } = new();

    private readonly Dictionary<long, ShoppingBudgetLedger> _shoppingBudgets = new();

    /// <summary>
    /// The shopping ledger for one wallet. Workers debit gold only when they reach the counter, so
    /// a second worker checking the raw wallet mid-trip would see money the first is already on its
    /// way to spend. Keyed by wallet rather than by contract because that is the resource actually
    /// contended: under a shared wallet every office draws on one ledger, and under separate
    /// wallets each owner gets their own and never constrains anyone else. (In single-player, and
    /// under the co-op shared-wallet setting, there is exactly one — see
    /// <see cref="Sponsor.WalletId"/>, which collapses every owner onto one id in that mode.)
    /// </summary>
    public ShoppingBudgetLedger ShoppingBudgetFor(long walletId)
    {
        if (!_shoppingBudgets.TryGetValue(walletId, out var ledger))
            _shoppingBudgets[walletId] = ledger = new ShoppingBudgetLedger();

        return ledger;
    }
}

/// <summary>
/// Runs one <see cref="ShiftOrchestrator"/> per active contract, so every office's farmhand works
/// the same day. Subscribes once to the game events and fans out to every live orchestrator
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

    /// <summary>The live shift working out of this office, if any.</summary>
    public ShiftOrchestrator? ForOffice(Guid officeId) =>
        _active.FirstOrDefault(o => o.ActiveOfficeId == officeId);

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

    private void PruneFinished() => _active.RemoveAll(o => o.ActiveContractId is null);

    private static bool MatchesContract(ContractId? id, string prefix)
    {
        if (id is null)
            return false;

        var normalized = prefix.Replace("-", "");
        return id.Value.Value.ToString("N").StartsWith(normalized, StringComparison.OrdinalIgnoreCase);
    }
}
