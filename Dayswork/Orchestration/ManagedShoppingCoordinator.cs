using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>
/// The managed-crop shopping trip: plan what to buy from the input-chest shortfall, walk the
/// worker to Pierre's/Joja (waiting outside a closed store), buy at the counter, walk home, and
/// settle purchases into the input chest. One instance per shift, held by the
/// <see cref="ShiftSession"/>; the travel→action→travel sequencing runs through the orchestrator's
/// travel primitive with <see cref="TravelPurpose.ShoppingStep"/>, dispatched back here via
/// <see cref="OnTravelArrived"/>.
/// </summary>
internal sealed class ManagedShoppingCoordinator
{
    private enum Phase
    {
        None,
        TravelingToStoreExterior,
        WalkingToWaitTile,
        WaitingForOpen,
        WalkingToEntrance,
        WalkingToCounter,
        Purchasing,
        TravelingToFarm,
        WalkingToInputChest,
    }

    internal enum WarpEdgeSourceKind
    {
        TileAction,
        MapProperty,
    }

    private sealed record RouteHop(
        GameLocation Source,
        GameLocation Target,
        TileCoord ApproachTile,
        TileCoord ArrivalTile,
        WarpEdgeSourceKind SourceKind);

    private sealed record WarpEdge(
        GameLocation Source,
        GameLocation Target,
        TileCoord ApproachTile,
        TileCoord ArrivalTile,
        string TargetName,
        WarpEdgeSourceKind SourceKind);

    private sealed record ActionWarp(
        int X,
        int Y,
        string TargetName,
        int TargetX,
        int TargetY);

    private sealed record StoreRoute(
        Store Store,
        GameLocation Exterior,
        GameLocation Interior,
        TileCoord EntranceTile,
        TileCoord WaitTile,
        TileCoord InteriorArrivalTile);

    private readonly ShiftSession _session;
    private readonly ShiftOrchestrator _host;
    private readonly WorkerMovementDriver _nav;
    private readonly ToolSwapAnimator _toolAnimator;
    private readonly ManagedCropFieldReader _cropFieldReader;
    private readonly ShiftSupplyAggregator _shiftSupplyAggregator;
    private readonly ShopStockReader _shopStockReader;
    private readonly PurchaseAffordabilityCalculator _purchaseAffordability;
    private readonly ShopPurchaseService _shopPurchaseService;

    private bool _attempted;
    private bool _inProgress;
    private bool _wrapAfterReturn;
    private bool _consolidated;
    private Phase _phase;
    private readonly Queue<StorePurchaseGroup> _groups = new();
    private StorePurchaseGroup? _group;
    private StoreRoute? _storeRoute;
    private readonly List<Item> _carriedItems = new();
    private readonly List<PurchaseLineOutcome> _outcomes = new();
    private int _waitTicks;
    private int _purchaseLineIndex;
    private int _purchaseFacing;
    private readonly List<PurchaseLineOutcome> _pendingOutcomes = new();

    public ManagedShoppingCoordinator(
        ShiftSession session,
        ShiftOrchestrator host,
        WorkerMovementDriver nav,
        ToolSwapAnimator toolAnimator,
        ManagedCropFieldReader cropFieldReader,
        ShiftSupplyAggregator shiftSupplyAggregator,
        ShopStockReader shopStockReader,
        PurchaseAffordabilityCalculator purchaseAffordability,
        ShopPurchaseService shopPurchaseService)
    {
        _session = session;
        _host = host;
        _nav = nav;
        _toolAnimator = toolAnimator;
        _cropFieldReader = cropFieldReader;
        _shiftSupplyAggregator = shiftSupplyAggregator;
        _shopStockReader = shopStockReader;
        _purchaseAffordability = purchaseAffordability;
        _shopPurchaseService = shopPurchaseService;
    }

    /// <summary>A shopping trip owns the worker's movement and blocks the normal intent dispatch.</summary>
    public bool IsInProgress => _inProgress;

    /// <summary>Parked at the wait tile until the store opens; the tick loop calls <see cref="ContinueWaitTick"/>.</summary>
    public bool IsWaitingForOpen => _phase == Phase.WaitingForOpen;

    /// <summary>Pacing through counter purchases one beat at a time; the tick loop calls <see cref="ContinuePurchaseTick"/>.</summary>
    public bool IsPurchasing => _phase == Phase.Purchasing;

    /// <summary>Re-arms the once-per-batch shopping attempt (called when a managed batch begins).</summary>
    public void ResetState()
    {
        _attempted = false;
        ClearRuntime(clearCarriedItems: true);
    }

    private void ClearRuntime(bool clearCarriedItems)
    {
        _inProgress = false;
        _wrapAfterReturn = false;
        _consolidated = false;
        _phase = Phase.None;
        _groups.Clear();
        _group = null;
        _storeRoute = null;
        _waitTicks = 0;
        _purchaseLineIndex = 0;
        _pendingOutcomes.Clear();
        if (clearCarriedItems)
            _carriedItems.Clear();
        _outcomes.Clear();
    }

    public bool TryStartIfNeeded(bool wrapAfterReturn)
    {
        if (_session.Worker is null || !_session.ManagedActive || _attempted || _inProgress)
            return false;

        if (!TryBuildPlan(out var affordable))
            return false;

        Start(affordable, wrapAfterReturn);
        return true;
    }

    private bool TryBuildPlan(out AffordablePurchasePlan affordable)
    {
        affordable = AffordablePurchasePlan.Empty;
        _attempted = true;

        var date = ShiftOrchestrator.CurrentManagedGameDate();
        if (Utility.isFestivalDay(date.Day, Game1.season))
        {
            CropHudNotifier.ShoppingFestivalSkipped();
            return false;
        }

        var inputChest = _host.TryGetInputChest();
        if (inputChest is null)
        {
            CropHudNotifier.ShoppingUnavailable();
            return false;
        }

        var fieldLocation = _host.ResolveManagedBatchLocation(_session.ManagedBatchLocationName) ?? Game1.getFarm();
        var fieldState = _cropFieldReader.Read(
            fieldLocation,
            date,
            _session.ManagedAssignments,
            _host.IsCurrentManagedBatchSeasonAgnostic());
        var supply = ShiftOrchestrator.ReadSupply(inputChest);
        var stock = _shopStockReader.ReadAll(date.Day, Game1.timeOfDay, includeClosedStock: true);
        var storePreference = _session.Ctx.WorkScopes.ManagedCrops?.BuyFromJojaFirst == true
            ? StorePreference.Joja : StorePreference.Pierre;
        var manifest = _shiftSupplyAggregator.BuildManifest(
            new CropPlan(_session.ManagedAssignments),
            fieldState,
            supply,
            storePreference,
            stock,
            isFestivalDay: false,
            debugLog: DevLog.Enabled ? msg => DevLog.Log($"[Dayswork][managed-crops][shopping] manifest-dbg: {msg}", LogLevel.Info) : null);

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] manifest resolved=[{string.Join(", ", manifest.Groups.SelectMany(g => g.Lines.Select(l => $"{l.ItemId}:{l.Quantity}@{g.Store}")))}]" +
            $" unavailable=[{string.Join(", ", manifest.ChestSupplyOnlyItems)}]",
            LogLevel.Info);

        return TryFinalizeManifest(manifest, storePreference, stock, date.Day, out affordable);
    }

    /// <summary>
    /// One shopping trip covering every managed location's shortfall, resolved up front before the
    /// managed-crop batches plant. Returns true when a trip departs; false when there's nothing to
    /// buy (festival, no chest, no shortfall, or unaffordable), in which case the caller proceeds
    /// straight to the batches.
    /// </summary>
    public bool TryStartConsolidatedTrip()
    {
        if (_session.Worker is null || _inProgress)
            return false;

        if (!TryBuildConsolidatedPlan(out var affordable))
            return false;

        Start(affordable, wrapAfterReturn: false);
        _consolidated = true;
        return true;
    }

    private bool TryBuildConsolidatedPlan(out AffordablePurchasePlan affordable)
    {
        affordable = AffordablePurchasePlan.Empty;

        var date = ShiftOrchestrator.CurrentManagedGameDate();
        if (Utility.isFestivalDay(date.Day, Game1.season))
        {
            CropHudNotifier.ShoppingFestivalSkipped();
            return false;
        }

        var inputChest = _host.TryGetInputChest();
        if (inputChest is null)
        {
            CropHudNotifier.ShoppingUnavailable();
            return false;
        }

        var assignments = _session.Ctx.WorkScopes.ManagedCrops?.Assignments
            ?? Array.Empty<CropZoneAssignment>();
        if (assignments.Count == 0)
            return false;

        // One live FieldState per managed location (the worker need not be present to read it).
        var fields = new List<FieldState>();
        foreach (var group in assignments.GroupBy(a => a.Zone.LocationName, StringComparer.Ordinal))
        {
            var location = _host.ResolveManagedBatchLocation(group.Key);
            if (location is null)
                continue;

            var locationAssignments = group.ToList();
            var seasonAgnostic = !string.Equals(group.Key, "Farm", StringComparison.Ordinal)
                || locationAssignments.Any(a => a.Mode == CropAssignmentMode.SeasonAgnostic);
            fields.Add(_cropFieldReader.Read(location, date, locationAssignments, seasonAgnostic));
        }

        if (fields.Count == 0)
            return false;

        var supply = ShiftOrchestrator.ReadSupply(inputChest);
        var stock = _shopStockReader.ReadAll(date.Day, Game1.timeOfDay, includeClosedStock: true);
        var storePreference = _session.Ctx.WorkScopes.ManagedCrops?.BuyFromJojaFirst == true
            ? StorePreference.Joja : StorePreference.Pierre;
        var manifest = _shiftSupplyAggregator.BuildManifest(
            new CropPlan(assignments),
            fields,
            supply,
            storePreference,
            stock,
            isFestivalDay: false,
            debugLog: DevLog.Enabled ? msg => DevLog.Log($"[Dayswork][managed-crops][shopping] manifest-dbg: {msg}", LogLevel.Info) : null);

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] consolidated manifest resolved=[{string.Join(", ", manifest.Groups.SelectMany(g => g.Lines.Select(l => $"{l.ItemId}:{l.Quantity}@{g.Store}")))}]" +
            $" unavailable=[{string.Join(", ", manifest.ChestSupplyOnlyItems)}]",
            LogLevel.Info);

        return TryFinalizeManifest(manifest, storePreference, stock, date.Day, out affordable);
    }

    /// <summary>Shared tail: fallback notice, wallet clamp, drop stores that can't open today.</summary>
    private bool TryFinalizeManifest(
        ShiftPurchaseManifest manifest,
        StorePreference storePreference,
        IReadOnlyList<ShopStockSnapshot> stock,
        int dayOfMonth,
        out AffordablePurchasePlan affordable)
    {
        affordable = AffordablePurchasePlan.Empty;

        if (!manifest.HasPurchases)
            return false;

        ShiftOrchestrator.NotifyFallbackStoreIfUsed(manifest, storePreference, stock);

        var walletClamped = _purchaseAffordability.ClampToWallet(manifest, Game1.player.Money);

        var groups = walletClamped.Groups
            .Where(group => StoreCanStillOpenToday(group.Store, dayOfMonth))
            .ToList();

        if (groups.Count == 0)
        {
            CropHudNotifier.ShoppingUnavailable();
            return false;
        }

        affordable = new AffordablePurchasePlan(groups, walletClamped.Shortfall);
        return affordable.HasPurchases;
    }

    private void Start(AffordablePurchasePlan affordable, bool wrapAfterReturn)
    {
        if (_session.Worker is null)
            return;

        ClearRuntime(clearCarriedItems: true);
        _attempted = true;
        _inProgress = true;
        _wrapAfterReturn = wrapAfterReturn;
        _session.CurrentManagedAction = null;
        _session.ManagedActions.Clear();
        _session.ActionPending = false;
        _toolAnimator.StopSwing();
        _session.Stuck.Reset();

        foreach (var group in affordable.Groups)
            _groups.Enqueue(group);

        CropHudNotifier.ShoppingDeparture();
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] departing groups={affordable.Groups.Count} estimatedCost={affordable.TotalCost} wrapAfterReturn={wrapAfterReturn}.",
            LogLevel.Info);

        StartNextStore();
    }

    private void StartNextStore()
    {
        if (_session.Worker is null)
            return;

        while (_groups.Count > 0)
        {
            _group = _groups.Dequeue();
            if (_group.Lines.Count == 0)
                continue;

            if (!TryResolveStoreRoute(_group.Store, out var route))
            {
                AbortTrip("store_route_unresolved");
                return;
            }

            _storeRoute = route;
            var current = _session.CurrentLocation ?? _session.Worker.currentLocation ?? Game1.getFarm();
            if (ShiftOrchestrator.SameLocation(current, route.Exterior))
            {
                _session.CurrentLocation = route.Exterior;
                BeginStoreExterior();
                return;
            }

            if (TryBuildRoute(current, route.Exterior, out var hops))
            {
                _phase = Phase.TravelingToStoreExterior;
                _host.StartTravel(BuildShoppingPlan(hops), TravelPurpose.ShoppingStep);
                return;
            }

            AbortTrip("store_route_path_unavailable");
            return;
        }

        BeginReturnToFarm();
    }

    /// <summary>Dispatches a completed ShoppingStep travel to the next phase of the trip.</summary>
    public void OnTravelArrived()
    {
        if (_session.Worker is null)
            return;

        switch (_phase)
        {
            case Phase.TravelingToStoreExterior:
                BeginStoreExterior();
                break;
            case Phase.WalkingToWaitTile:
                BeginWait();
                break;
            case Phase.WalkingToEntrance:
                BeginCounterWalk();
                break;
            case Phase.WalkingToCounter:
                BuyCurrentGroup();
                break;
            case Phase.TravelingToFarm:
                BeginInputChestWalk();
                break;
            case Phase.WalkingToInputChest:
                CompleteReturn();
                break;
            default:
                AbortTrip($"unexpected_phase_{_phase}");
                break;
        }
    }

    /// <summary>A ShoppingStep travel failed; abort with the current phase in the reason.</summary>
    public void AbortTripForNavigationFailure() => AbortTrip($"navigation_failed_{_phase}");

    private static TravelPlan BuildShoppingPlan(IEnumerable<RouteHop> hops)
    {
        var legs = hops
            .Select(hop => new TravelLeg(hop.Source, hop.ApproachTile, hop.Target, hop.ArrivalTile))
            .ToList();
        return new TravelPlan(legs, TravelFailurePolicy.ReportFailure);
    }

    private void BeginStoreExterior()
    {
        if (_storeRoute is null)
        {
            AbortTrip("missing_store_route");
            return;
        }

        var date = ShiftOrchestrator.CurrentManagedGameDate();
        if (StoreHoursPolicy.IsOpen(_storeRoute.Store, Game1.timeOfDay, date.Day))
        {
            StartEntranceWalk();
            return;
        }

        if (!StoreCanStillOpenToday(_storeRoute.Store, date.Day))
        {
            CropHudNotifier.ShoppingUnavailable();
            StartNextStore();
            return;
        }

        _phase = Phase.WalkingToWaitTile;
        _host.StartTravel(
            ShiftOrchestrator.WalkOnlyPlan(_storeRoute.Exterior, _storeRoute.WaitTile),
            TravelPurpose.ShoppingStep);
    }

    private void BeginWait()
    {
        if (_storeRoute is null)
        {
            AbortTrip("missing_wait_store");
            return;
        }

        _phase = Phase.WaitingForOpen;
        _waitTicks = 0;
        CropHudNotifier.WaitingForStoreOpen(StoreDisplayName(_storeRoute.Store));
        ContinueWaitTick();
    }

    /// <summary>One throttled tick of the parked-outside-a-closed-store wait loop.</summary>
    public void ContinueWaitTick()
    {
        if (_session.Worker is null || _storeRoute is null)
            return;

        var date = ShiftOrchestrator.CurrentManagedGameDate();
        if (StoreHoursPolicy.IsOpen(_storeRoute.Store, Game1.timeOfDay, date.Day))
        {
            StartEntranceWalk();
            return;
        }

        if (!StoreCanStillOpenToday(_storeRoute.Store, date.Day))
        {
            CropHudNotifier.ShoppingUnavailable();
            StartNextStore();
            return;
        }

        _waitTicks++;
        if (_waitTicks % 30 == 1)
            _session.Worker.doEmote(ShiftOrchestrator.EmoteMusic);
    }

    private void StartEntranceWalk()
    {
        if (_storeRoute is null || _session.Worker is null)
            return;

        // One leg: walk to the store door, warp through to the interior arrival tile.
        var route = _storeRoute;
        _phase = Phase.WalkingToEntrance;
        _host.StartTravel(
            new TravelPlan(
                new[] { new TravelLeg(route.Exterior, route.EntranceTile, route.Interior, route.InteriorArrivalTile) },
                TravelFailurePolicy.ReportFailure),
            TravelPurpose.ShoppingStep);
    }

    private void BeginCounterWalk()
    {
        if (_storeRoute is null || _session.Worker is null)
            return;

        _session.CurrentLocation = _storeRoute.Interior;
        var counterTile = FindStoreCounterStandTile(_storeRoute.Interior, _storeRoute.Store);
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] counter selected store={_storeRoute.Store} " +
            $"interior={LocationKey(_storeRoute.Interior)} tile=({counterTile.X},{counterTile.Y}).",
            LogLevel.Info);

        _phase = Phase.WalkingToCounter;
        _host.StartTravel(
            ShiftOrchestrator.WalkOnlyPlan(_storeRoute.Interior, counterTile),
            TravelPurpose.ShoppingStep);
    }

    private void BuyCurrentGroup()
    {
        if (_group is null)
        {
            StartNextStore();
            return;
        }

        _phase = Phase.Purchasing;
        _purchaseLineIndex = 0;
        _purchaseFacing = ResolvePurchaseFacing();
        _toolAnimator.PlaySwing(WorkerTool.None, _purchaseFacing);
    }

    public void ContinuePurchaseTick()
    {
        if (_toolAnimator.IsSwinging)
            return;

        if (_group is null)
        {
            FinishPurchasing();
            return;
        }

        var lines = _group.Lines;
        while (_purchaseLineIndex < lines.Count && lines[_purchaseLineIndex].Quantity <= 0)
            _purchaseLineIndex++;

        if (_purchaseLineIndex >= lines.Count)
        {
            FinishPurchasing();
            return;
        }

        var outcome = _shopPurchaseService.BuyLineToCarriedItems(
            _group.Store, lines[_purchaseLineIndex], Game1.player, _carriedItems);
        if (outcome is null)
        {
            AbortTrip("purchase_bind_failed");
            return;
        }

        _pendingOutcomes.Add(outcome);

        if (_storeRoute is { Interior: { } interior } && Game1.player.currentLocation == interior)
            interior.playSound("purchaseClick");

        _purchaseLineIndex++;

        if (_purchaseLineIndex < lines.Count)
            _toolAnimator.PlaySwing(WorkerTool.None, _purchaseFacing);
        else
            FinishPurchasing();
    }

    private int ResolvePurchaseFacing()
    {
        var worker = _session.Worker;
        if (worker is null || _group is null || _storeRoute is not { Interior: { } interior })
            return worker?.FacingDirection ?? 2;

        var workerPt = worker.TilePoint;
        var candidates = FindShopActionTiles(interior, _group.Store).ToList();
        if (candidates.Count == 0)
            return worker.FacingDirection;

        var nearest = candidates.OrderBy(t => Math.Abs(t.X - workerPt.X) + Math.Abs(t.Y - workerPt.Y)).First();
        return ShiftOrchestrator.FacingToward(workerPt, nearest, worker.FacingDirection);
    }

    private void FinishPurchasing()
    {
        var outcomes = _pendingOutcomes.ToList();
        _pendingOutcomes.Clear();
        _outcomes.AddRange(outcomes);

        if (outcomes.Any(o => o.BoughtQty > 0))
            CropHudNotifier.ShoppingPurchaseItems(outcomes);
        if (outcomes.Any(o => o.Kind is PurchaseOutcomeKind.Partial or PurchaseOutcomeKind.Insufficient))
            CropHudNotifier.InsufficientFunds();

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] store={_group?.Store} bought={outcomes.Sum(o => o.BoughtQty)} spent={outcomes.Sum(o => o.SpentGold)} goldRemaining={Game1.player.Money}.",
            LogLevel.Info);

        _group = null;
        _storeRoute = null;
        StartNextStore();
    }

    private void BeginReturnToFarm()
    {
        if (_session.Worker is null)
            return;

        CropHudNotifier.ShoppingReturning();
        var farm = Game1.getFarm();
        var current = _session.CurrentLocation ?? _session.Worker.currentLocation ?? farm;
        if (ShiftOrchestrator.SameLocation(current, farm))
        {
            _session.CurrentLocation = farm;
            BeginInputChestWalk();
            return;
        }

        if (TryBuildRoute(current, farm, out var hops))
        {
            _phase = Phase.TravelingToFarm;
            _host.StartTravel(BuildShoppingPlan(hops), TravelPurpose.ShoppingStep);
            return;
        }

        AbortTrip("return_route_unavailable");
    }

    private void BeginInputChestWalk()
    {
        if (_session.Worker is null)
            return;

        var farm = Game1.getFarm();
        _session.CurrentLocation = farm;
        if (_carriedItems.Count == 0)
        {
            CompleteReturn();
            return;
        }

        if (HiringBuilding.TryGetInputChestTile(farm) is not { } chestPoint ||
            !ShiftOrchestrator.TrySelectChestDepositStandTile(new TileCoord(chestPoint.X, chestPoint.Y), farm, _session.Worker, out var standTile))
        {
            SettleCarriedItems(showHud: true);
            CompleteReturn();
            return;
        }

        _phase = Phase.WalkingToInputChest;
        _host.StartTravel(ShiftOrchestrator.WalkOnlyPlan(farm, standTile), TravelPurpose.ShoppingStep);
    }

    private void CompleteReturn()
    {
        var wrapAfterReturn = _wrapAfterReturn;
        var consolidated = _consolidated;
        SettleCarriedItems(showHud: true);
        ClearRuntime(clearCarriedItems: false);
        _attempted = true;
        _nav.Clear();

        if (wrapAfterReturn || _host.ShouldWrapUpBeforeNextUnit())
        {
            _host.QueueWrapUpNow(_session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        // The up-front consolidated trip bought for every location before any managed batch ran, so
        // it hands straight back to the batch loop (worker is already on the farm) rather than
        // re-entering a single batch's location.
        if (consolidated)
        {
            _host.ResumeAfterConsolidatedShopping();
            return;
        }

        // Farm batches resume in place; building batches walk back in through the door first
        // (ManagedReentry travel), then ResumeManagedBatchAfterShopping re-plans and continues.
        if (string.Equals(_session.ManagedBatchLocationName, "Farm", StringComparison.Ordinal))
        {
            _session.CurrentLocation = Game1.getFarm();
            _host.ResumeManagedBatchAfterShopping();
            return;
        }

        if (!_host.TryStartManagedReentryTravel())
            _host.CompleteManagedCropBatch();
    }

    private void AbortTrip(string reason)
    {
        DevLog.Log($"[Dayswork][managed-crops][shopping] aborted reason={reason}.", DevLog.WarnLevel);
        _host.CancelActiveTravel();
        CropHudNotifier.ShoppingUnavailable();
        WarpWorkerToFarm();
        SettleCarriedItems(showHud: false);
        CompleteReturn();
    }

    private void WarpWorkerToFarm()
    {
        if (_session.Worker is null)
            return;

        var farm = Game1.getFarm();
        var current = _session.Worker.currentLocation ?? _session.CurrentLocation ?? farm;
        if (ShiftOrchestrator.SameLocation(current, farm))
        {
            _session.CurrentLocation = farm;
            _nav.Clear();
            return;
        }

        _nav.WarpWorker(_session.Worker, current, farm, _session.FarmExitTile);
        _session.CurrentLocation = farm;
    }

    /// <summary>
    /// Deposits carried purchases into the input chest; anything that doesn't fit (or has no chest)
    /// goes to the shift overflow so items are never lost.
    /// </summary>
    public void SettleCarriedItems(bool showHud)
    {
        if (_carriedItems.Count == 0)
            return;

        var inputChest = _host.TryGetInputChest();
        var deposited = 0;
        foreach (var item in _carriedItems)
        {
            if (inputChest is null)
            {
                AddOverflow(item, OverflowReason.ChestMissing);
                continue;
            }

            var before = Math.Max(1, item.Stack);
            var leftover = inputChest.addItem(item);
            var rejected = Math.Max(0, leftover?.Stack ?? 0);
            deposited += Math.Max(0, before - rejected);
            if (leftover is not null && leftover.Stack > 0)
                AddOverflow(leftover, OverflowReason.ChestFull);
        }

        inputChest?.clearNulls();
        _carriedItems.Clear();
        if (showHud && deposited > 0)
            CropHudNotifier.ShoppingDeposited(deposited);
    }

    private void AddOverflow(Item item, OverflowReason reason)
    {
        if (string.IsNullOrWhiteSpace(item.QualifiedItemId) || item.Stack <= 0)
            return;

        _session.Ctx.Overflow.Add(new OverflowItem(
            new RoutedItemStack(
                item.QualifiedItemId,
                item.Stack,
                TaskKind.HarvestCrops,
                OutputScopeProvenance.Outdoor()),
            reason));
    }

    private bool TryResolveStoreRoute(Store store, out StoreRoute route)
    {
        route = null!;
        var interiorName = StoreInteriorName(store);
        var interior = Game1.getLocationFromName(interiorName);
        if (interior is null)
            return false;

        var preferredExteriors = ResolvePreferredStoreExteriors(store).ToList();
        var candidates = EnumerateStoreRouteCandidates(store, interiorName, interior, preferredExteriors).ToList();
        if (candidates.Count == 0)
        {
            LogStoreRouteUnresolved(store, interiorName, interior, preferredExteriors);
            return false;
        }

        route = candidates
            .OrderBy(candidate => StoreExteriorRank(store, candidate.Exterior))
            .ThenBy(candidate => candidate.EntranceTile.Y)
            .ThenByDescending(candidate => candidate.EntranceTile.X)
            .FirstOrDefault()!;
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] store route selected store={store} exterior={LocationKey(route.Exterior)} " +
            $"interior={LocationKey(route.Interior)} entrance=({route.EntranceTile.X},{route.EntranceTile.Y}) " +
            $"wait=({route.WaitTile.X},{route.WaitTile.Y}) arrival=({route.InteriorArrivalTile.X},{route.InteriorArrivalTile.Y}).",
            LogLevel.Info);
        return route is not null;
    }

    private static IEnumerable<GameLocation> ResolvePreferredStoreExteriors(Store store)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in StoreExteriorNames(store))
        {
            var location = Game1.getLocationFromName(name);
            if (location is not null && seen.Add(LocationKey(location)))
                yield return location;
        }
    }

    private static IEnumerable<StoreRoute> EnumerateStoreRouteCandidates(
        Store store,
        string interiorName,
        GameLocation interior,
        IEnumerable<GameLocation> exteriors)
    {
        foreach (var exterior in exteriors)
        {
            foreach (var edge in EnumerateWarpEdges(exterior))
            {
                if (!string.Equals(edge.TargetName, interiorName, StringComparison.OrdinalIgnoreCase) &&
                    !ShiftOrchestrator.SameLocation(edge.Target, interior))
                    continue;

                var wait = ResolvePassableNearbyInLocation(new TileCoord(edge.ApproachTile.X + 1, edge.ApproachTile.Y), exterior);
                yield return new StoreRoute(store, exterior, interior, edge.ApproachTile, wait, edge.ArrivalTile);
            }
        }
    }

    private static int StoreExteriorRank(Store store, GameLocation exterior)
    {
        var key = LocationKey(exterior);
        var rank = 0;
        foreach (var name in StoreExteriorNames(store))
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return rank;

            rank++;
        }

        return int.MaxValue;
    }

    private static void LogStoreRouteUnresolved(
        Store store,
        string interiorName,
        GameLocation interior,
        IReadOnlyCollection<GameLocation> preferredExteriors)
    {
        var preferredNames = StoreExteriorNames(store)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        var loadedPreferred = preferredExteriors
            .Select(LocationKey)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        var preferredKeys = preferredExteriors
            .Select(LocationKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignoredCandidates = EnumerateStoreRouteCandidates(
                store,
                interiorName,
                interior,
                EnumerateWorldLocations().Where(location => !preferredKeys.Contains(LocationKey(location))))
            .Select(candidate =>
                $"{LocationKey(candidate.Exterior)}@({candidate.EntranceTile.X},{candidate.EntranceTile.Y})->{LocationKey(candidate.Interior)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .Take(20);

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] store route unresolved store={store} interior={interiorName} " +
            $"preferredExteriors=[{string.Join(", ", preferredNames)}] loadedPreferred=[{string.Join(", ", loadedPreferred)}] " +
            $"ignoredNonPublicCandidates=[{string.Join(", ", ignoredCandidates)}].",
            DevLog.WarnLevel);
    }

    private bool TryBuildRoute(
        GameLocation source,
        GameLocation target,
        out Queue<RouteHop> route)
    {
        // Prefer routes with at least one intermediate hop. This prevents a direct
        // farm-edge shortcut (e.g. Grandpa's Farm right edge → Town at (139,22)) from
        // winning over the natural bus-stop path just because the worker happened to be
        // near that edge when shopping was triggered. Fall back to a direct hop only when
        // no intermediate route exists.
        return TryBuildRouteCore(source, target, out route, skipDirectToTarget: true)
            || TryBuildRouteCore(source, target, out route, skipDirectToTarget: false);
    }

    private bool TryBuildRouteCore(
        GameLocation source,
        GameLocation target,
        out Queue<RouteHop> route,
        bool skipDirectToTarget)
    {
        route = new Queue<RouteHop>();
        if (ShiftOrchestrator.SameLocation(source, target))
            return true;

        var locations = EnumerateWorldLocations()
            .GroupBy(LocationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var sourceKey = LocationKey(source);
        var targetKey = LocationKey(target);
        locations[sourceKey] = source;
        locations[targetKey] = target;

        var queue = new Queue<string>();
        var cameFrom = new Dictionary<string, (string Previous, RouteHop Hop)?>(StringComparer.OrdinalIgnoreCase)
        {
            [sourceKey] = null,
        };
        queue.Enqueue(sourceKey);

        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase))
                break;

            if (!locations.TryGetValue(key, out var location))
                continue;

            foreach (var edge in OrderWarpEdges(location))
            {
                var nextKey = LocationKey(edge.Target);
                if (cameFrom.ContainsKey(nextKey))
                    continue;

                if (skipDirectToTarget
                    && string.Equals(key, sourceKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(nextKey, targetKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                locations[nextKey] = edge.Target;

                var arrivalTile = edge.ArrivalTile;
                var hop = new RouteHop(
                    edge.Source,
                    edge.Target,
                    edge.ApproachTile,
                    arrivalTile,
                    edge.SourceKind);
                cameFrom[nextKey] = (key, hop);
                queue.Enqueue(nextKey);
            }
        }

        if (!cameFrom.ContainsKey(targetKey))
        {
            if (!skipDirectToTarget)
                LogRouteUnavailable(source, target, locations);
            return false;
        }

        var stack = new Stack<RouteHop>();
        var current = targetKey;
        while (cameFrom[current] is { } entry)
        {
            stack.Push(entry.Hop);
            current = entry.Previous;
        }

        foreach (var hop in stack)
            route.Enqueue(hop);

        LogRouteSelected(source, target, route);
        return true;
    }

    private IEnumerable<WarpEdge> OrderWarpEdges(GameLocation location)
    {
        var edges = EnumerateWarpEdges(location).ToList();
        if (_session.Worker is null || !ShiftOrchestrator.SameLocation(_session.Worker.currentLocation ?? location, location))
            return OrderWarpEdgesByTargetPriority(
                edges,
                TargetRank,
                edge => edge.SourceKind,
                edge => edge.ApproachTile);

        var source = new TileCoord(_session.Worker.TilePoint.X, _session.Worker.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, location);
        return OrderWarpEdgesByRoutePriority(
            edges,
            edge => routeCosts.TryGetValue(edge.ApproachTile, out var cost) ? cost : int.MaxValue,
            TargetRank,
            edge => edge.SourceKind,
            edge => edge.ApproachTile);
    }

    internal static IEnumerable<T> OrderWarpEdgesByRoutePriority<T>(
        IEnumerable<T> edges,
        Func<T, int> routeCost,
        Func<T, int> targetRank,
        Func<T, WarpEdgeSourceKind> sourceKind,
        Func<T, TileCoord> approachTile) =>
        edges
            .OrderBy(routeCost)
            .ThenBy(targetRank)
            .ThenBy(edge => SourceKindRank(sourceKind(edge)))
            .ThenBy(edge => approachTile(edge).Y)
            .ThenBy(edge => approachTile(edge).X);

    internal static IEnumerable<T> OrderWarpEdgesByTargetPriority<T>(
        IEnumerable<T> edges,
        Func<T, int> targetRank,
        Func<T, WarpEdgeSourceKind> sourceKind,
        Func<T, TileCoord> approachTile) =>
        edges
            .OrderBy(targetRank)
            .ThenBy(edge => SourceKindRank(sourceKind(edge)))
            .ThenBy(edge => approachTile(edge).Y)
            .ThenBy(edge => approachTile(edge).X);

    private static int SourceKindRank(WarpEdgeSourceKind sourceKind) =>
        sourceKind == WarpEdgeSourceKind.TileAction ? 0 : 1;

    private TileCoord FindStoreCounterStandTile(GameLocation interior, Store store)
    {
        if (_session.Worker is not null)
        {
            var source = new TileCoord(_session.Worker.TilePoint.X, _session.Worker.TilePoint.Y);
            var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, interior);
            if (WorkerRouteSelector.TrySelectNearestReachableTile(
                    FindShopStandCandidates(interior, store),
                    routeCosts,
                    out var standTile))
                return standTile;
        }

        var fallback = store == Store.Pierre ? new TileCoord(4, 17) : new TileCoord(13, 23);
        return ResolvePassableNearbyInLocation(fallback, interior);
    }

    private static IEnumerable<TileCoord> FindShopStandCandidates(GameLocation interior, Store store)
    {
        foreach (var actionTile in FindShopActionTiles(interior, store))
        {
            foreach (var stand in ShiftOrchestrator.DepositStandTilesAround(actionTile))
            {
                if (WorkerMovementDriver.IsTilePassableForWorker(new Point(stand.Tile.X, stand.Tile.Y), interior))
                    yield return stand.Tile;
            }
        }
    }

    private static IEnumerable<TileCoord> FindShopActionTiles(GameLocation interior, Store store)
    {
        if (interior.Map?.Layers.Count is null or 0)
            yield break;

        var layer = interior.Map.Layers[0];
        var layerNames = new[] { "Buildings", "Back" };
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                foreach (var layerName in layerNames)
                {
                    if (interior.Map.GetLayer(layerName) is null)
                        continue;

                    var action = interior.doesTileHaveProperty(x, y, "Action", layerName, false);
                    if (HasStoreShopAction(action, store))
                        yield return new TileCoord(x, y);
                }
            }
        }
    }

    private static bool HasStoreShopAction(string? action, Store store)
    {
        if (string.IsNullOrWhiteSpace(action))
            return false;

        var tokens = action.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        // "JojaShop" is Joja's tile action (handled by GameLocation.checkAction separately from OpenShop)
        if (string.Equals(tokens[0], "JojaShop", StringComparison.OrdinalIgnoreCase))
            return store == Store.Joja;

        if (string.Equals(tokens[0], "Buy", StringComparison.OrdinalIgnoreCase))
            return store == Store.Pierre
                   && tokens.Length >= 2
                   && string.Equals(tokens[1], "General", StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(tokens[0], "OpenShop", StringComparison.OrdinalIgnoreCase) || tokens.Length < 2)
            return false;

        return store switch
        {
            Store.Pierre => string.Equals(tokens[1], PierreShopActionId, StringComparison.OrdinalIgnoreCase),
            Store.Joja => string.Equals(tokens[1], JojaShopActionId, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static IEnumerable<GameLocation> EnumerateWorldLocations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in new[]
                 {
                     Game1.getFarm(),
                     Game1.getLocationFromName("BusStop"),
                     Game1.getLocationFromName("Town"),
                     Game1.getLocationFromName("SeedShop"),
                     Game1.getLocationFromName("JojaMart"),
                 })
        {
            if (location is not null && seen.Add(LocationKey(location)))
                yield return location;
        }

        foreach (var location in Game1.locations)
        {
            if (location is not null && seen.Add(LocationKey(location)))
                yield return location;
        }
    }

    private static IEnumerable<WarpEdge> EnumerateWarpEdges(GameLocation location)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var warp in location.warps)
        {
            var edge = CreateWarpEdge(
                location,
                warp.X,
                warp.Y,
                warp.TargetName,
                warp.TargetX,
                warp.TargetY,
                WarpEdgeSourceKind.MapProperty);
            if (edge is not null &&
                seen.Add($"{edge.TargetName}|{edge.ApproachTile.X}|{edge.ApproachTile.Y}|{edge.ArrivalTile.X}|{edge.ArrivalTile.Y}"))
                yield return edge;
        }

        foreach (var actionWarp in EnumerateActionWarps(location))
        {
            var edge = CreateWarpEdge(
                location,
                actionWarp.X,
                actionWarp.Y,
                actionWarp.TargetName,
                actionWarp.TargetX,
                actionWarp.TargetY,
                WarpEdgeSourceKind.TileAction);
            if (edge is not null &&
                seen.Add($"{edge.TargetName}|{edge.ApproachTile.X}|{edge.ApproachTile.Y}|{edge.ArrivalTile.X}|{edge.ArrivalTile.Y}"))
                yield return edge;
        }
    }

    private static WarpEdge? CreateWarpEdge(
        GameLocation source,
        int x,
        int y,
        string targetName,
        int targetX,
        int targetY,
        WarpEdgeSourceKind sourceKind)
    {
        var target = ResolveWarpTarget(targetName);
        if (target is null)
            return null;

        return new WarpEdge(
            source,
            target,
            ResolveWarpApproachTile(source, x, y),
            ResolvePassableNearbyInLocation(new TileCoord(targetX, targetY), target),
            targetName,
            sourceKind);
    }

    private static IEnumerable<ActionWarp> EnumerateActionWarps(GameLocation location)
    {
        if (location.Map?.Layers.Count is null or 0)
            yield break;

        var layer = location.Map.Layers[0];
        var layerNames = new[] { "Buildings", "Back" };
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                foreach (var layerName in layerNames)
                {
                    if (location.Map.GetLayer(layerName) is null)
                        continue;

                    foreach (var propertyName in WarpPropertyNames)
                    {
                        var action = location.doesTileHaveProperty(x, y, propertyName, layerName, false);
                        if (TryParseActionWarp(action, out var targetName, out var targetX, out var targetY))
                            yield return new ActionWarp(x, y, targetName, targetX, targetY);
                    }
                }
            }
        }
    }

    private static bool TryParseActionWarp(string? action, out string targetName, out int targetX, out int targetY)
    {
        targetName = string.Empty;
        targetX = 0;
        targetY = 0;

        if (string.IsNullOrWhiteSpace(action))
            return false;

        var tokens = action.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 4)
            return false;

        if (string.Equals(tokens[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(tokens[1], out targetX) && int.TryParse(tokens[2], out targetY))
            {
                targetName = tokens[3];
                return !string.IsNullOrWhiteSpace(targetName);
            }

            return false;
        }

        if (string.Equals(tokens[0], "LoadMap", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(tokens[2], out targetX) && int.TryParse(tokens[3], out targetY))
            {
                targetName = tokens[1];
                return !string.IsNullOrWhiteSpace(targetName);
            }

            return false;
        }

        if (string.Equals(tokens[0], "Warp", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(tokens[1], out targetX) && int.TryParse(tokens[2], out targetY))
            {
                targetName = tokens[3];
                return !string.IsNullOrWhiteSpace(targetName);
            }

            if (int.TryParse(tokens[2], out targetX) && int.TryParse(tokens[3], out targetY))
            {
                targetName = tokens[1];
                return !string.IsNullOrWhiteSpace(targetName);
            }
        }

        return false;
    }

    private static void LogRouteSelected(
        GameLocation source,
        GameLocation target,
        IEnumerable<RouteHop> route)
    {
        var hops = route
            .Select(hop =>
                $"{LocationKey(hop.Source)}->{LocationKey(hop.Target)} source={hop.SourceKind} approach=({hop.ApproachTile.X},{hop.ApproachTile.Y}) arrival=({hop.ArrivalTile.X},{hop.ArrivalTile.Y})")
            .ToList();
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] route selected source={LocationKey(source)} target={LocationKey(target)} " +
            $"hops=[{string.Join("; ", hops)}].",
            LogLevel.Info);
    }

    private static GameLocation? ResolveWarpTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        if (string.Equals(targetName, "Farm", StringComparison.OrdinalIgnoreCase))
            return Game1.getFarm();

        return Game1.getLocationFromName(targetName);
    }

    private static TileCoord ResolveWarpApproachTile(GameLocation location, int warpX, int warpY)
    {
        if (location.Map?.Layers.Count is null or 0)
            return new TileCoord(warpX, warpY);

        var layer = location.Map.Layers[0];
        var cx = Math.Clamp(warpX, 0, layer.LayerWidth - 1);
        var cy = Math.Clamp(warpY, 0, layer.LayerHeight - 1);
        var dx = warpX >= layer.LayerWidth ? -1 : warpX < 0 ? 1 : 0;
        var dy = warpY >= layer.LayerHeight ? -1 : warpY < 0 ? 1 : 0;

        if (dx != 0 || dy != 0)
        {
            for (var step = 0; step < 10; step++)
            {
                var x = cx + dx * step;
                var y = cy + dy * step;
                if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight)
                    break;

                if (WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), location))
                    return new TileCoord(x, y);
            }
        }

        return ResolvePassableNearbyInLocation(new TileCoord(cx, cy), location);
    }

    private static void LogRouteUnavailable(
        GameLocation source,
        GameLocation target,
        IReadOnlyDictionary<string, GameLocation> locations)
    {
        var locationNames = locations.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(80);
        var edges = EnumerateWorldLocations()
            .SelectMany(location => EnumerateWarpEdges(location)
                .Select(edge =>
                    $"{LocationKey(edge.Source)}->{LocationKey(edge.Target)}@({edge.ApproachTile.X},{edge.ApproachTile.Y})"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(edge => edge, StringComparer.OrdinalIgnoreCase)
            .Take(120);

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] route unavailable source={LocationKey(source)} target={LocationKey(target)} " +
            $"locations=[{string.Join(", ", locationNames)}] edges=[{string.Join(", ", edges)}].",
            DevLog.WarnLevel);
    }

    private static TileCoord ResolvePassableNearbyInLocation(TileCoord preferred, GameLocation location)
    {
        if (location.Map?.Layers.Count is null or 0)
            return preferred;

        var layer = location.Map.Layers[0];
        var width = layer.LayerWidth;
        var height = layer.LayerHeight;

        bool IsPassable(int x, int y) =>
            x >= 0
            && y >= 0
            && x < width
            && y < height
            && WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), location);

        if (IsPassable(preferred.X, preferred.Y))
            return preferred;

        const int MaxRadius = 12;
        for (var radius = 1; radius <= MaxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var x = preferred.X + dx;
                    var y = preferred.Y + dy;
                    if (IsPassable(x, y))
                        return new TileCoord(x, y);
                }
            }
        }

        return preferred;
    }

    private static bool StoreCanStillOpenToday(Store store, int dayOfMonth)
    {
        if (StoreHoursPolicy.OpensAt(store, dayOfMonth) is null)
            return false;

        return Game1.timeOfDay < StoreCloseTime(store);
    }

    private static int StoreCloseTime(Store store) =>
        store == Store.Pierre ? StoreHoursPolicy.PierreCloseTime : StoreHoursPolicy.JojaCloseTime;

    private static string StoreInteriorName(Store store) =>
        store == Store.Pierre ? "SeedShop" : "JojaMart";

    private const string PierreShopActionId = "SeedShop";
    private const string JojaShopActionId = "Joja";

    private static readonly string[] WarpPropertyNames = { "Action", "TouchAction" };

    private static int TargetRank(WarpEdge edge) =>
        edge.TargetName switch
        {
            "Town" => 0,
            "BusStop" => 1,
            "SeedShop" => 2,
            "JojaMart" => 2,
            _ => 10,
        };

    private static string[] StoreExteriorNames(Store store) =>
        store switch
        {
            Store.Pierre => new[] { "Town" },
            Store.Joja => new[] { "Town" },
            _ => Array.Empty<string>(),
        };

    private static string StoreDisplayName(Store store) =>
        store == Store.Pierre ? "Pierre's" : "JojaMart";

    private static string LocationKey(GameLocation location) =>
        ShiftOrchestrator.LocationKey(location);
}
