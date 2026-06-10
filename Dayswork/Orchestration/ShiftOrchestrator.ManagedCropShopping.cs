using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator
{
    // The travel→action→travel sequencer for a shopping trip. Each phase's travel is started with
    // TravelPurpose.ShoppingStep; OnManagedShoppingTravelArrived dispatches on the phase when it
    // completes (buy at the counter, walk to the next store, return home, settle the input chest).
    private enum ManagedShoppingPhase
    {
        None,
        TravelingToStoreExterior,
        WalkingToWaitTile,
        WaitingForOpen,
        WalkingToEntrance,
        WalkingToCounter,
        TravelingToFarm,
        WalkingToInputChest,
    }

    private sealed record ManagedShoppingRouteHop(
        GameLocation Source,
        GameLocation Target,
        TileCoord ApproachTile,
        TileCoord ArrivalTile);

    private sealed record ManagedShoppingWarpEdge(
        GameLocation Source,
        GameLocation Target,
        TileCoord ApproachTile,
        TileCoord ArrivalTile,
        string TargetName);

    private sealed record ManagedShoppingActionWarp(
        int X,
        int Y,
        string TargetName,
        int TargetX,
        int TargetY);

    private sealed record ManagedStoreRoute(
        Store Store,
        GameLocation Exterior,
        GameLocation Interior,
        TileCoord EntranceTile,
        TileCoord WaitTile,
        TileCoord InteriorArrivalTile);

    private bool _managedShoppingAttempted;
    private bool _managedShoppingInProgress;
    private bool _managedShoppingWrapAfterReturn;
    private ManagedShoppingPhase _managedShoppingPhase;
    private readonly Queue<StorePurchaseGroup> _managedShoppingGroups = new();
    private StorePurchaseGroup? _managedShoppingGroup;
    private ManagedStoreRoute? _managedShoppingStoreRoute;
    private readonly List<Item> _managedShoppingCarriedItems = new();
    private readonly List<PurchaseLineOutcome> _managedShoppingOutcomes = new();
    private int _managedShoppingWaitTicks;

    private void ResetManagedShoppingState()
    {
        _managedShoppingAttempted = false;
        ClearManagedShoppingRuntime(clearCarriedItems: true);
    }

    private void ClearManagedShoppingRuntime(bool clearCarriedItems)
    {
        _managedShoppingInProgress = false;
        _managedShoppingWrapAfterReturn = false;
        _managedShoppingPhase = ManagedShoppingPhase.None;
        _managedShoppingGroups.Clear();
        _managedShoppingGroup = null;
        _managedShoppingStoreRoute = null;
        _managedShoppingWaitTicks = 0;
        if (clearCarriedItems)
            _managedShoppingCarriedItems.Clear();
        _managedShoppingOutcomes.Clear();
    }

    private bool TryStartManagedShoppingIfNeeded(bool wrapAfterReturn)
    {
        if (_ctx is null || _farmhand is null || !_managedActive || _managedShoppingAttempted || _managedShoppingInProgress)
            return false;

        if (!TryBuildManagedShoppingPlan(out var affordable))
            return false;

        StartManagedShopping(affordable, wrapAfterReturn);
        return true;
    }

    private bool TryBuildManagedShoppingPlan(out AffordablePurchasePlan affordable)
    {
        affordable = AffordablePurchasePlan.Empty;
        _managedShoppingAttempted = true;

        var date = CurrentManagedGameDate();
        if (Utility.isFestivalDay(date.Day, Game1.season))
        {
            CropHudNotifier.ShoppingFestivalSkipped();
            return false;
        }

        var inputChest = TryGetInputChest();
        if (inputChest is null)
        {
            CropHudNotifier.ShoppingUnavailable();
            return false;
        }

        var fieldLocation = ResolveManagedBatchLocation(_managedBatchLocationName) ?? Game1.getFarm();
        var fieldState = _cropFieldReader.Read(
            fieldLocation,
            date,
            _managedAssignments,
            IsCurrentManagedBatchSeasonAgnostic());
        var supply = ReadSupply(inputChest);
        var stock = _shopStockReader.ReadAll(date.Day, Game1.timeOfDay, includeClosedStock: true);
        var manifest = _shiftSupplyAggregator.BuildManifest(
            new CropPlan(_managedAssignments),
            fieldState,
            supply,
            ModEntry.PreferredCropStore,
            stock,
            isFestivalDay: false);

        if (!manifest.HasPurchases)
            return false;

        NotifyFallbackStoreIfUsed(manifest);

        var walletClamped = _purchaseAffordability.ClampToWallet(manifest, Game1.player.Money);
        if (walletClamped.Shortfall)
            CropHudNotifier.InsufficientFunds();

        var groups = walletClamped.Groups
            .Where(group => StoreCanStillOpenToday(group.Store, date.Day))
            .ToList();

        if (groups.Count == 0)
        {
            CropHudNotifier.ShoppingUnavailable();
            return false;
        }

        affordable = new AffordablePurchasePlan(groups, walletClamped.Shortfall);
        return affordable.HasPurchases;
    }

    private void StartManagedShopping(AffordablePurchasePlan affordable, bool wrapAfterReturn)
    {
        if (_ctx is null || _farmhand is null)
            return;

        ClearManagedShoppingRuntime(clearCarriedItems: true);
        _managedShoppingAttempted = true;
        _managedShoppingInProgress = true;
        _managedShoppingWrapAfterReturn = wrapAfterReturn;
        _currentManagedAction = null;
        _managedActions.Clear();
        _actionPending = false;
        _toolAnimator.StopSwing();
        _stuck.Reset();

        foreach (var group in affordable.Groups)
            _managedShoppingGroups.Enqueue(group);

        CropHudNotifier.ShoppingDeparture();
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] departing groups={affordable.Groups.Count} estimatedCost={affordable.TotalCost} wrapAfterReturn={wrapAfterReturn}.",
            LogLevel.Info);

        StartNextManagedShoppingStore();
    }

    private void StartNextManagedShoppingStore()
    {
        if (_ctx is null || _farmhand is null)
            return;

        while (_managedShoppingGroups.Count > 0)
        {
            _managedShoppingGroup = _managedShoppingGroups.Dequeue();
            if (_managedShoppingGroup.Lines.Count == 0)
                continue;

            if (!TryResolveStoreRoute(_managedShoppingGroup.Store, out var route))
            {
                AbortManagedShoppingTrip("store_route_unresolved");
                return;
            }

            _managedShoppingStoreRoute = route;
            var current = _currentLocation ?? _farmhand.currentLocation ?? Game1.getFarm();
            if (SameLocation(current, route.Exterior))
            {
                _currentLocation = route.Exterior;
                BeginManagedShoppingStoreExterior();
                return;
            }

            if (TryBuildManagedShoppingRoute(current, route.Exterior, out var hops))
            {
                _managedShoppingPhase = ManagedShoppingPhase.TravelingToStoreExterior;
                StartTravel(BuildShoppingPlan(hops), TravelPurpose.ShoppingStep);
                return;
            }

            AbortManagedShoppingTrip("store_route_path_unavailable");
            return;
        }

        BeginManagedShoppingReturnToFarm();
    }

    /// <summary>Dispatches a completed ShoppingStep travel to the next phase of the trip.</summary>
    private void OnManagedShoppingTravelArrived()
    {
        if (_ctx is null || _farmhand is null)
            return;

        switch (_managedShoppingPhase)
        {
            case ManagedShoppingPhase.TravelingToStoreExterior:
                BeginManagedShoppingStoreExterior();
                break;
            case ManagedShoppingPhase.WalkingToWaitTile:
                BeginManagedShoppingWait();
                break;
            case ManagedShoppingPhase.WalkingToEntrance:
                BeginManagedShoppingCounterWalk();
                break;
            case ManagedShoppingPhase.WalkingToCounter:
                BuyManagedShoppingGroup();
                break;
            case ManagedShoppingPhase.TravelingToFarm:
                BeginManagedShoppingInputChestWalk();
                break;
            case ManagedShoppingPhase.WalkingToInputChest:
                CompleteManagedShoppingReturn();
                break;
            default:
                AbortManagedShoppingTrip($"unexpected_phase_{_managedShoppingPhase}");
                break;
        }
    }

    private static TravelPlan BuildShoppingPlan(IEnumerable<ManagedShoppingRouteHop> hops)
    {
        var legs = hops
            .Select(hop => new TravelLeg(hop.Source, hop.ApproachTile, hop.Target, hop.ArrivalTile))
            .ToList();
        return new TravelPlan(legs, TravelFailurePolicy.ReportFailure);
    }

    private void BeginManagedShoppingStoreExterior()
    {
        if (_managedShoppingStoreRoute is null)
        {
            AbortManagedShoppingTrip("missing_store_route");
            return;
        }

        var date = CurrentManagedGameDate();
        if (StoreHoursPolicy.IsOpen(_managedShoppingStoreRoute.Store, Game1.timeOfDay, date.Day))
        {
            StartManagedShoppingEntranceWalk();
            return;
        }

        if (!StoreCanStillOpenToday(_managedShoppingStoreRoute.Store, date.Day))
        {
            CropHudNotifier.ShoppingUnavailable();
            StartNextManagedShoppingStore();
            return;
        }

        _managedShoppingPhase = ManagedShoppingPhase.WalkingToWaitTile;
        StartTravel(
            WalkOnlyPlan(_managedShoppingStoreRoute.Exterior, _managedShoppingStoreRoute.WaitTile),
            TravelPurpose.ShoppingStep);
    }

    private void BeginManagedShoppingWait()
    {
        if (_managedShoppingStoreRoute is null)
        {
            AbortManagedShoppingTrip("missing_wait_store");
            return;
        }

        _managedShoppingPhase = ManagedShoppingPhase.WaitingForOpen;
        _managedShoppingWaitTicks = 0;
        CropHudNotifier.WaitingForStoreOpen(StoreDisplayName(_managedShoppingStoreRoute.Store));
        ContinueManagedShoppingWait();
    }

    private void ContinueManagedShoppingWait()
    {
        if (_farmhand is null || _managedShoppingStoreRoute is null)
            return;

        var date = CurrentManagedGameDate();
        if (StoreHoursPolicy.IsOpen(_managedShoppingStoreRoute.Store, Game1.timeOfDay, date.Day))
        {
            StartManagedShoppingEntranceWalk();
            return;
        }

        if (!StoreCanStillOpenToday(_managedShoppingStoreRoute.Store, date.Day))
        {
            CropHudNotifier.ShoppingUnavailable();
            StartNextManagedShoppingStore();
            return;
        }

        _managedShoppingWaitTicks++;
        if (_managedShoppingWaitTicks % 30 == 1)
            _farmhand.doEmote(EmoteMusic);
    }

    private void StartManagedShoppingEntranceWalk()
    {
        if (_managedShoppingStoreRoute is null || _farmhand is null)
            return;

        // One leg: walk to the store door, warp through to the interior arrival tile.
        var route = _managedShoppingStoreRoute;
        _managedShoppingPhase = ManagedShoppingPhase.WalkingToEntrance;
        StartTravel(
            new TravelPlan(
                new[] { new TravelLeg(route.Exterior, route.EntranceTile, route.Interior, route.InteriorArrivalTile) },
                TravelFailurePolicy.ReportFailure),
            TravelPurpose.ShoppingStep);
    }

    private void BeginManagedShoppingCounterWalk()
    {
        if (_managedShoppingStoreRoute is null || _farmhand is null)
            return;

        _currentLocation = _managedShoppingStoreRoute.Interior;
        var counterTile = FindStoreCounterStandTile(_managedShoppingStoreRoute.Interior, _managedShoppingStoreRoute.Store);
        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] counter selected store={_managedShoppingStoreRoute.Store} " +
            $"interior={LocationKey(_managedShoppingStoreRoute.Interior)} tile=({counterTile.X},{counterTile.Y}).",
            LogLevel.Info);

        _managedShoppingPhase = ManagedShoppingPhase.WalkingToCounter;
        StartTravel(
            WalkOnlyPlan(_managedShoppingStoreRoute.Interior, counterTile),
            TravelPurpose.ShoppingStep);
    }

    private void BuyManagedShoppingGroup()
    {
        if (_managedShoppingGroup is null)
        {
            StartNextManagedShoppingStore();
            return;
        }

        var result = _shopPurchaseService.BuyToCarriedItems(
            _managedShoppingGroup,
            Game1.player,
            _managedShoppingCarriedItems);
        if (result.BindFailed)
        {
            AbortManagedShoppingTrip("purchase_bind_failed");
            return;
        }

        _managedShoppingOutcomes.AddRange(result.Outcomes);
        var bought = result.Outcomes.Sum(outcome => outcome.BoughtQty);
        var spent = result.TotalSpentGold;
        if (bought > 0)
            CropHudNotifier.ShoppingPurchaseSummary(bought, spent);
        if (result.AnyShortfall)
            CropHudNotifier.InsufficientFunds();

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] store={_managedShoppingGroup.Store} bought={bought} spent={spent} goldRemaining={Game1.player.Money}.",
            LogLevel.Info);

        _managedShoppingGroup = null;
        _managedShoppingStoreRoute = null;
        StartNextManagedShoppingStore();
    }

    private void BeginManagedShoppingReturnToFarm()
    {
        if (_farmhand is null)
            return;

        CropHudNotifier.ShoppingReturning();
        var farm = Game1.getFarm();
        var current = _currentLocation ?? _farmhand.currentLocation ?? farm;
        if (SameLocation(current, farm))
        {
            _currentLocation = farm;
            BeginManagedShoppingInputChestWalk();
            return;
        }

        if (TryBuildManagedShoppingRoute(current, farm, out var hops))
        {
            _managedShoppingPhase = ManagedShoppingPhase.TravelingToFarm;
            StartTravel(BuildShoppingPlan(hops), TravelPurpose.ShoppingStep);
            return;
        }

        AbortManagedShoppingTrip("return_route_unavailable");
    }

    private void BeginManagedShoppingInputChestWalk()
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        _currentLocation = farm;
        if (_managedShoppingCarriedItems.Count == 0)
        {
            CompleteManagedShoppingReturn();
            return;
        }

        if (HiringBuilding.TryGetInputChestTile(farm) is not { } chestPoint ||
            !TrySelectChestDepositStandTile(new TileCoord(chestPoint.X, chestPoint.Y), farm, _farmhand, out var standTile))
        {
            SettleManagedShoppingCarriedItems(showHud: true);
            CompleteManagedShoppingReturn();
            return;
        }

        _managedShoppingPhase = ManagedShoppingPhase.WalkingToInputChest;
        StartTravel(WalkOnlyPlan(farm, standTile), TravelPurpose.ShoppingStep);
    }

    private void CompleteManagedShoppingReturn()
    {
        var wrapAfterReturn = _managedShoppingWrapAfterReturn;
        SettleManagedShoppingCarriedItems(showHud: true);
        ClearManagedShoppingRuntime(clearCarriedItems: false);
        _managedShoppingAttempted = true;
        _nav.Clear();

        if (_ctx is null)
            return;

        if (wrapAfterReturn || ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(_ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        // Farm batches resume in place; building batches walk back in through the door first
        // (ManagedReentry travel), then ResumeManagedBatchAfterShopping re-plans and continues.
        if (string.Equals(_managedBatchLocationName, "Farm", StringComparison.Ordinal))
        {
            _currentLocation = Game1.getFarm();
            ResumeManagedBatchAfterShopping();
            return;
        }

        if (!TryStartManagedReentryTravel())
            CompleteManagedCropBatch();
    }

    private void AbortManagedShoppingTrip(string reason)
    {
        DevLog.Log($"[Dayswork][managed-crops][shopping] aborted reason={reason}.", LogLevel.Warn);
        CancelActiveTravel();
        CropHudNotifier.ShoppingUnavailable();
        WarpManagedShoppingWorkerToFarm();
        SettleManagedShoppingCarriedItems(showHud: false);
        CompleteManagedShoppingReturn();
    }

    private void WarpManagedShoppingWorkerToFarm()
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        var current = _farmhand.currentLocation ?? _currentLocation ?? farm;
        if (SameLocation(current, farm))
        {
            _currentLocation = farm;
            _nav.Clear();
            return;
        }

        _nav.WarpWorker(_farmhand, current, farm, _farmExitTile);
        _currentLocation = farm;
    }

    private void SettleManagedShoppingCarriedItems(bool showHud)
    {
        if (_ctx is null || _managedShoppingCarriedItems.Count == 0)
            return;

        var inputChest = TryGetInputChest();
        var deposited = 0;
        foreach (var item in _managedShoppingCarriedItems)
        {
            if (inputChest is null)
            {
                AddManagedShoppingOverflow(item, OverflowReason.ChestMissing);
                continue;
            }

            var before = Math.Max(1, item.Stack);
            var leftover = inputChest.addItem(item);
            var rejected = Math.Max(0, leftover?.Stack ?? 0);
            deposited += Math.Max(0, before - rejected);
            if (leftover is not null && leftover.Stack > 0)
                AddManagedShoppingOverflow(leftover, OverflowReason.ChestFull);
        }

        inputChest?.clearNulls();
        _managedShoppingCarriedItems.Clear();
        if (showHud && deposited > 0)
            CropHudNotifier.ShoppingDeposited(deposited);
    }

    private void AddManagedShoppingOverflow(Item item, OverflowReason reason)
    {
        if (_ctx is null || string.IsNullOrWhiteSpace(item.QualifiedItemId) || item.Stack <= 0)
            return;

        _ctx.Overflow.Add(new OverflowItem(
            new RoutedItemStack(
                item.QualifiedItemId,
                item.Stack,
                TaskKind.HarvestCrops,
                OutputScopeProvenance.Outdoor()),
            reason));
    }

    private bool TryResolveStoreRoute(Store store, out ManagedStoreRoute route)
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
            LogManagedShoppingStoreRouteUnresolved(store, interiorName, interior, preferredExteriors);
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

    private static IEnumerable<ManagedStoreRoute> EnumerateStoreRouteCandidates(
        Store store,
        string interiorName,
        GameLocation interior,
        IEnumerable<GameLocation> exteriors)
    {
        foreach (var exterior in exteriors)
        {
            foreach (var edge in EnumerateManagedShoppingWarpEdges(exterior))
            {
                if (!string.Equals(edge.TargetName, interiorName, StringComparison.OrdinalIgnoreCase) &&
                    !SameLocation(edge.Target, interior))
                    continue;

                var wait = ResolvePassableNearbyInLocation(new TileCoord(edge.ApproachTile.X + 1, edge.ApproachTile.Y), exterior);
                yield return new ManagedStoreRoute(store, exterior, interior, edge.ApproachTile, wait, edge.ArrivalTile);
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

    private static void LogManagedShoppingStoreRouteUnresolved(
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
            LogLevel.Warn);
    }

    private bool TryBuildManagedShoppingRoute(
        GameLocation source,
        GameLocation target,
        out Queue<ManagedShoppingRouteHop> route)
    {
        route = new Queue<ManagedShoppingRouteHop>();
        if (SameLocation(source, target))
            return true;

        var locations = EnumerateWorldLocations()
            .GroupBy(LocationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var sourceKey = LocationKey(source);
        var targetKey = LocationKey(target);
        locations[sourceKey] = source;
        locations[targetKey] = target;

        var queue = new Queue<string>();
        var cameFrom = new Dictionary<string, (string Previous, ManagedShoppingRouteHop Hop)?>(StringComparer.OrdinalIgnoreCase)
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

            foreach (var edge in OrderManagedShoppingWarpEdges(location))
            {
                var nextKey = LocationKey(edge.Target);
                if (cameFrom.ContainsKey(nextKey))
                    continue;

                locations[nextKey] = edge.Target;

                // Pin the farm-side endpoint: when a hop lands back on the farm, drop the worker on
                // the canonical _farmExitTile (the same tile they spawned at / every other return uses)
                // instead of the warp's debris-spiral ArrivalTile. Keeps the return landing predictable
                // and consistent day-to-day even as overnight debris shifts which nearby tiles are open.
                var arrivalTile = SameLocation(edge.Target, Game1.getFarm())
                    ? _farmExitTile
                    : edge.ArrivalTile;
                var hop = new ManagedShoppingRouteHop(
                    edge.Source,
                    edge.Target,
                    edge.ApproachTile,
                    arrivalTile);
                cameFrom[nextKey] = (key, hop);
                queue.Enqueue(nextKey);
            }
        }

        if (!cameFrom.ContainsKey(targetKey))
        {
            LogManagedShoppingRouteUnavailable(source, target, locations);
            return false;
        }

        var stack = new Stack<ManagedShoppingRouteHop>();
        var current = targetKey;
        while (cameFrom[current] is { } entry)
        {
            stack.Push(entry.Hop);
            current = entry.Previous;
        }

        foreach (var hop in stack)
            route.Enqueue(hop);

        LogManagedShoppingRouteSelected(source, target, route);
        return true;
    }

    private IEnumerable<ManagedShoppingWarpEdge> OrderManagedShoppingWarpEdges(GameLocation location)
    {
        var edges = EnumerateManagedShoppingWarpEdges(location).ToList();
        if (_farmhand is null || !SameLocation(_farmhand.currentLocation ?? location, location))
            return edges.OrderBy(ManagedShoppingTargetRank).ThenBy(edge => edge.ApproachTile.Y).ThenBy(edge => edge.ApproachTile.X);

        var source = new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, location);
        return edges
            .OrderBy(edge => routeCosts.TryGetValue(edge.ApproachTile, out var cost) ? cost : int.MaxValue)
            .ThenBy(ManagedShoppingTargetRank)
            .ThenBy(edge => edge.ApproachTile.Y)
            .ThenBy(edge => edge.ApproachTile.X);
    }

    private TileCoord FindStoreCounterStandTile(GameLocation interior, Store store)
    {
        if (_farmhand is not null)
        {
            var source = new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y);
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
            foreach (var stand in DepositStandTilesAround(actionTile))
            {
                if (WorkerMovementDriver.IsTilePassableForWorker(new Point(stand.X, stand.Y), interior))
                    yield return stand;
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

    private static IEnumerable<ManagedShoppingWarpEdge> EnumerateManagedShoppingWarpEdges(GameLocation location)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var warp in location.warps)
        {
            var edge = CreateManagedShoppingWarpEdge(
                location,
                warp.X,
                warp.Y,
                warp.TargetName,
                warp.TargetX,
                warp.TargetY);
            if (edge is not null &&
                seen.Add($"{edge.TargetName}|{edge.ApproachTile.X}|{edge.ApproachTile.Y}|{edge.ArrivalTile.X}|{edge.ArrivalTile.Y}"))
                yield return edge;
        }

        foreach (var actionWarp in EnumerateActionWarps(location))
        {
            var edge = CreateManagedShoppingWarpEdge(
                location,
                actionWarp.X,
                actionWarp.Y,
                actionWarp.TargetName,
                actionWarp.TargetX,
                actionWarp.TargetY);
            if (edge is not null &&
                seen.Add($"{edge.TargetName}|{edge.ApproachTile.X}|{edge.ApproachTile.Y}|{edge.ArrivalTile.X}|{edge.ArrivalTile.Y}"))
                yield return edge;
        }
    }

    private static ManagedShoppingWarpEdge? CreateManagedShoppingWarpEdge(
        GameLocation source,
        int x,
        int y,
        string targetName,
        int targetX,
        int targetY)
    {
        var target = ResolveWarpTarget(targetName);
        if (target is null)
            return null;

        return new ManagedShoppingWarpEdge(
            source,
            target,
            ResolveWarpApproachTile(source, x, y),
            ResolvePassableNearbyInLocation(new TileCoord(targetX, targetY), target),
            targetName);
    }

    private static IEnumerable<ManagedShoppingActionWarp> EnumerateActionWarps(GameLocation location)
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

                    foreach (var propertyName in ManagedShoppingWarpPropertyNames)
                    {
                        var action = location.doesTileHaveProperty(x, y, propertyName, layerName, false);
                        if (TryParseActionWarp(action, out var targetName, out var targetX, out var targetY))
                            yield return new ManagedShoppingActionWarp(x, y, targetName, targetX, targetY);
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

    private static void LogManagedShoppingRouteSelected(
        GameLocation source,
        GameLocation target,
        IEnumerable<ManagedShoppingRouteHop> route)
    {
        var hops = route
            .Select(hop =>
                $"{LocationKey(hop.Source)}->{LocationKey(hop.Target)} approach=({hop.ApproachTile.X},{hop.ApproachTile.Y}) arrival=({hop.ArrivalTile.X},{hop.ArrivalTile.Y})")
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

    private static void LogManagedShoppingRouteUnavailable(
        GameLocation source,
        GameLocation target,
        IReadOnlyDictionary<string, GameLocation> locations)
    {
        var locationNames = locations.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(80);
        var edges = EnumerateWorldLocations()
            .SelectMany(location => EnumerateManagedShoppingWarpEdges(location)
                .Select(edge =>
                    $"{LocationKey(edge.Source)}->{LocationKey(edge.Target)}@({edge.ApproachTile.X},{edge.ApproachTile.Y})"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(edge => edge, StringComparer.OrdinalIgnoreCase)
            .Take(120);

        DevLog.Log(
            $"[Dayswork][managed-crops][shopping] route unavailable source={LocationKey(source)} target={LocationKey(target)} " +
            $"locations=[{string.Join(", ", locationNames)}] edges=[{string.Join(", ", edges)}].",
            LogLevel.Warn);
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

    private static readonly string[] ManagedShoppingWarpPropertyNames = { "Action", "TouchAction" };

    private static int ManagedShoppingTargetRank(ManagedShoppingWarpEdge edge) =>
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
        location.NameOrUniqueName ?? location.Name;

    private static bool SameLocation(GameLocation left, GameLocation right) =>
        string.Equals(LocationKey(left), LocationKey(right), StringComparison.OrdinalIgnoreCase);
}
