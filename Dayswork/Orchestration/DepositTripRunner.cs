using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// Executes the planned deposit trips: walks trip-by-trip to each chest / the shipping bin
/// (entering buildings through their doors), deposits stack-by-stack on the animation beat, and
/// routes every failure (chest missing/busy/full, unreachable destination) to the shift overflow
/// so items are never lost. One instance per shift, held by the <see cref="ShiftSession"/>.
/// The orchestrator plans the trips (BeginDeposit) and owns the phase transitions
/// (BeginWrapUp / Exiting); the runner reports back by calling <see cref="ShiftOrchestrator.BeginExit"/>
/// when the last trip completes.
/// </summary>
internal sealed class DepositTripRunner
{
    private readonly ShiftSession _session;
    private readonly ShiftOrchestrator _host;
    private readonly WorkerMovementDriver _nav;
    private readonly ToolSwapAnimator _toolAnimator;
    private readonly ChestResolver _chestResolver;
    private readonly BuildingWorkNavigator _buildingNavigator;

    private readonly Queue<DepositTrip> _trips = new();
    private DepositTrip? _current;

    // Per-stack paced execution within the in-flight trip. Each stack is one beat gated by
    // _toolAnimator.IsSwinging (duration == WorkerActionAnimationMs), so the deposit cadence
    // tracks the same config knob as task actions.
    private int           _stackIndex;
    private bool          _executionStarted;
    private Chest?        _chest;
    private GameLocation? _location;
    private bool          _chestAnimated;   // we triggered the open-lid animation

    public DepositTripRunner(
        ShiftSession session,
        ShiftOrchestrator host,
        WorkerMovementDriver nav,
        ToolSwapAnimator toolAnimator,
        ChestResolver chestResolver,
        BuildingWorkNavigator buildingNavigator)
    {
        _session = session;
        _host = host;
        _nav = nav;
        _toolAnimator = toolAnimator;
        _chestResolver = chestResolver;
        _buildingNavigator = buildingNavigator;
    }

    public bool HasPending => _trips.Count > 0;

    /// <summary>Replaces the queue with a freshly planned trip list.</summary>
    public void Load(IReadOnlyList<DepositTrip> trips)
    {
        _trips.Clear();
        foreach (var trip in trips)
            _trips.Enqueue(trip);
        _current = null;
        _executionStarted = false;
        _stackIndex = 0;
        _chest = null;
        _location = null;
        _chestAnimated = false;
    }

    /// <summary>Dequeues the next trip and makes it the in-flight one.</summary>
    public DepositTrip BeginNextTrip()
    {
        var next = _trips.Dequeue();
        _current = next;
        return next;
    }

    /// <summary>The per-tick handler while the shift intent is a deposit intent.</summary>
    public void HandleDepositTick(Farm farm)
    {
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        if (_current is null)
        {
            // Nothing in flight — advance to the next trip or exit.
            FinalizeAndAdvanceTrip(farm);
            return;
        }

        if (_nav.NavigationFailed)
        {
            MarkTripUndelivered(_current);
            FinalizeAndAdvanceTrip(farm);
            return;
        }

        // First arrival at the trip's stand tile: open the chest visually (if applicable),
        // resolve mutex/liveness, and start the first beat. The mutex/missing-chest paths
        // already route the items to overflow inside BeginTripExecution and return false so we skip the loop.
        if (!_executionStarted)
        {
            if (!BeginTripExecution(_current, _session.CurrentLocation ?? farm))
            {
                FinalizeAndAdvanceTrip(farm);
            }
            return;
        }

        // Pacing gate: wait for the per-stack swing beat to finish before depositing the next stack.
        if (_toolAnimator.IsSwinging)
            return;

        if (_stackIndex < _current.Items.Count)
        {
            DepositCurrentStack();
            _stackIndex++;
            if (_stackIndex < _current.Items.Count)
            {
                // Kick off the next beat by replaying the no-tool reach animation.
                _toolAnimator.PlaySwing(WorkerTool.None, FacingTowardDestination());
            }
            return;
        }

        EndTripExecution();

        // If we deposited inside a building, walk to the interior door before leaving;
        // the warp back to the farm happens once the worker reaches the door.
        if (BeginInteriorExitWalk())
            return;

        FinalizeAndAdvanceTrip(farm);
    }

    private bool BeginInteriorExitWalk()
    {
        if (_session.Worker is null ||
            _current is not { Destination: ChestDestination { Ref.LocationName: not "Farm" } chestDest })
            return false;

        if (_current.Destination is ChestDestination expansionChest &&
            ModEntry.ExpansionCompat is { } compat &&
            compat.IsExpansionDepositLocation(expansionChest.Ref.LocationName))
        {
            var routeSource = (_session.CurrentLocation ?? _session.Worker.currentLocation)?.NameOrUniqueName
                              ?? expansionChest.Ref.LocationName;
            if (_host.TryStartExpansionTravel(
                    routeSource,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    TravelFailurePolicy.WarpToDestination,
                    TravelPurpose.DepositExit))
                return true;

            _host.WarpExpansionWorkerToFarm();
            return false;
        }

        var interior = _session.CurrentLocation;
        if (interior is null || interior == Game1.getFarm())
            return false;

        var farmArrival = _buildingNavigator.TryResolveDoorTile(chestDest.Ref.LocationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : _session.FarmExitTile;
        _host.StartTravel(_host.BuildBuildingExitPlan(interior, farmArrival), TravelPurpose.DepositExit);
        return true;
    }

    public void FinalizeAndAdvanceTrip(Farm farm)
    {
        if (_current is not null)
            CompleteTripLocation(_current);

        _current = null;
        _executionStarted = false;
        _stackIndex = 0;
        _chest = null;
        _location = null;
        _chestAnimated = false;

        if (_trips.Count > 0)
        {
            var next = _trips.Dequeue();
            _current = next;
            _session.Ctx.StateMachine.SetIntent(ShiftOrchestrator.ToDepositIntent(next));
            StartTravelToTrip(next);
            return;
        }

        if (_session.ResumeIdleAfterDeposit)
            _host.OnPreIdleDepositComplete();
        else
            _host.BeginExit(farm);
    }

    /// <summary>The DepositEntry travel arrived inside the trip's building; walk to the chest.</summary>
    public void OnDepositEntryTravelArrived()
    {
        if (_current is not { Destination: ChestDestination chestDest })
        {
            FinalizeAndAdvanceTrip(Game1.getFarm());
            return;
        }

        _session.Ctx.StateMachine.SetIntent(ShiftOrchestrator.ToDepositIntent(_current));
        StartChestDepositNavigation(_current, chestDest, _session.CurrentLocation ?? Game1.getFarm());
    }

    /// <summary>The DepositEntry travel failed; the in-flight trip is undeliverable.</summary>
    public void OnDepositEntryTravelFailed(Farm farm)
    {
        if (_current is not null)
            MarkTripUndelivered(_current);
        FinalizeAndAdvanceTrip(farm);
    }

    private bool BeginTripExecution(DepositTrip trip, GameLocation location)
    {
        _executionStarted = true;
        _stackIndex = 0;
        _chest = null;
        _location = location;
        _chestAnimated = false;

        if (trip.Destination is ChestDestination chestDest)
        {
            var chest = _chestResolver.ResolveChest(chestDest.Ref);
            if (chest is null)
            {
                // Chest moved/destroyed: everything for it goes to automatic overflow.
                foreach (var stack in trip.Items)
                    _session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest missing at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) routed to automatic overflow.",
                    LogLevel.Trace);
                return false;
            }

            if (chest.GetMutex().IsLocked())
            {
                // A farmer (player) has the chest UI open. Defer the whole trip to overflow
                // rather than mutating items behind the player's back.
                foreach (var stack in trip.Items)
                    _session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestBusy));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest busy at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) routed to automatic overflow.",
                    LogLevel.Trace);
                return false;
            }

            _chest = chest;

            // Only animate / play sound if the player is in the chest's location.
            // SDV's location.playSound is already location-scoped; the playerHere guard also
            // skips the lid frame mutation when nobody is around to see it.
            if (chest.Location is { } chestLoc && Game1.player.currentLocation == chestLoc)
            {
                chest.frameCounter.Value = 5;  // vanilla open trigger
                chestLoc.playSound("openChest", new Vector2(chest.TileLocation.X, chest.TileLocation.Y));
                _chestAnimated = true;
            }
        }
        // (Shipping bin: per-stack Farm.shipItem handles the bin lid + sound; no trip-level open.)

        // Start the first beat: a no-tool reach animation facing the destination tile.
        _toolAnimator.PlaySwing(WorkerTool.None, FacingTowardDestination());
        return true;
    }

    private void DepositCurrentStack()
    {
        if (_current is null)
            return;

        var stack = _current.Items[_stackIndex];
        var loc = _location;
        var playerHere = loc is not null && Game1.player.currentLocation == loc;

        if (_chest is { } chest)
        {
            // Re-check the mutex per stack: if the player just opened the chest UI in the
            // middle of our deposit, abort the rest of the trip and route what remains to overflow.
            if (chest.GetMutex().IsLocked())
            {
                for (var i = _stackIndex; i < _current.Items.Count; i++)
                    _session.Ctx.Overflow.Add(new OverflowItem(_current.Items[i], OverflowReason.ChestBusy));
                _stackIndex = _current.Items.Count;  // skip ahead to "trip complete"
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest became busy mid-trip; remaining stacks routed to automatic overflow.",
                    LogLevel.Trace);
                return;
            }

            DepositIntoChest(chest, stack);
            if (playerHere && chest.Location is { } chestLoc)
                chestLoc.playSound("Ship", new Vector2(chest.TileLocation.X, chest.TileLocation.Y));
            return;
        }

        DepositIntoShippingBin(stack, animateWhenPlayerHere: playerHere);
    }

    private static bool IsDepositErrorItem(Item item) =>
        item is null
        || string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);

    // Reconstruct the actual item for a deposit/ship: a captured flavored/colored item (roe, wine…)
    // is cloned from the per-shift registry so its identity and price survive; everything else is the
    // ordinary id-based create with quality applied.
    private Item? CreateDepositItem(RoutedItemStack stack)
    {
        if (stack.FlavorId is { } flavorId
            && FlavorItemRegistry.Rebuild(_session.Flavors.Templates, flavorId, stack.Quantity, stack.Quality) is { } flavored)
            return flavored;

        var item = ItemRegistry.Create(stack.QualifiedItemId, stack.Quantity);
        if (item is SObject obj && stack.Quality > 0)
            obj.Quality = stack.Quality;
        return item;
    }

    private void DepositIntoShippingBin(RoutedItemStack stack, bool animateWhenPlayerHere)
    {
        var farm = Game1.getFarm();
        if (string.IsNullOrWhiteSpace(stack.QualifiedItemId))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at shipping bin: empty QualifiedItemId stack=x{stack.Quantity} task={stack.SourceTask}; not shipped.",
                LogLevel.Error);
            return;
        }
        var item = CreateDepositItem(stack);
        if (item is null)
            return;
        if (IsDepositErrorItem(item))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at shipping bin: rawId='{stack.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' stack=x{stack.Quantity} task={stack.SourceTask}; not shipped.",
                LogLevel.Error);
            return;
        }

        if (animateWhenPlayerHere && Game1.player.currentLocation == farm)
            farm.shipItem(item, Game1.player);           // vanilla lid animation + backpackIN + delayed "Ship"
        else
            farm.getShippingBin(Game1.player).Add(item); // silent fallback
    }

    private void EndTripExecution()
    {
        if (_chest is { } chest && _chestAnimated)
        {
            // Vanilla close trigger: the chest's per-tick update will animate the lid down
            // and emit the "doorClose" sound on completion.
            chest.frameCounter.Value = -1;
        }
    }

    private void MarkTripUndelivered(DepositTrip trip)
    {
        RouteUndeliveredTripStacks(trip, trip.Items);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][deposit] could not reach deposit destination at ({trip.Tile.X},{trip.Tile.Y}); routed {trip.Items.Count} undelivered stack(s).",
            DevLog.WarnLevel);
    }

    private void DepositIntoChest(Chest chest, RoutedItemStack stack)
    {
        if (string.IsNullOrWhiteSpace(stack.QualifiedItemId))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at chest: empty QualifiedItemId stack=x{stack.Quantity} task={stack.SourceTask}; not deposited.",
                LogLevel.Error);
            return;
        }
        var item = CreateDepositItem(stack);
        if (item is null)
            return;
        if (IsDepositErrorItem(item))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at chest: rawId='{stack.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' stack=x{stack.Quantity} task={stack.SourceTask}; not deposited.",
                LogLevel.Error);
            return;
        }

        // addItem returns the remainder that did not fit (null if all fit).
        var leftover = chest.addItem(item);
        if (leftover is not null && leftover.Stack > 0)
        {
            // Chest full: route the remainder to automatic overflow.
            _session.Ctx.Overflow.Add(new OverflowItem(
                new RoutedItemStack(stack.QualifiedItemId, leftover.Stack, stack.SourceTask, stack.Provenance, stack.Quality, stack.FlavorId),
                OverflowReason.ChestFull));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] chest full; {leftover.Stack}x {stack.QualifiedItemId} routed to automatic overflow.",
                LogLevel.Trace);
        }
    }

    /// <summary>Navigates / travels to the trip's destination (entering buildings as needed).</summary>
    public void StartTravelToTrip(DepositTrip trip)
    {
        if (_session.Worker is null)
            return;

        var farm = Game1.getFarm();
        if (trip.Destination is ChestDestination { Ref.LocationName: not "Farm" } chestDest)
        {
            if (ModEntry.ExpansionCompat is { } compat &&
                compat.IsExpansionDepositLocation(chestDest.Ref.LocationName))
            {
                var source = (_session.CurrentLocation ?? farm).NameOrUniqueName;
                if (_host.TryStartExpansionTravel(
                        source,
                        chestDest.Ref.LocationName,
                        ExpansionRoutePurpose.DepositEntry,
                        TravelFailurePolicy.ReportFailure,
                        TravelPurpose.DepositEntry))
                    return;

                MarkTripUndelivered(trip);
                FinalizeAndAdvanceTrip(farm);
                return;
            }

            if (_host.TryBuildBuildingEntryPlan(
                    chestDest.Ref.LocationName,
                    TravelFailurePolicy.WarpToDestination,
                    out var plan))
            {
                _host.StartTravel(plan, TravelPurpose.DepositEntry);
                return;
            }

            foreach (var stack in trip.Items)
                _session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
            _current = null;
            return;
        }

        _session.CurrentLocation = farm;
        if (trip.Destination is ChestDestination farmChest)
        {
            StartChestDepositNavigation(trip, farmChest, farm);
            return;
        }

        _nav.StartNavigation(trip.Tile, farm, _session.Worker);
    }

    private void StartChestDepositNavigation(DepositTrip trip, ChestDestination chestDest, GameLocation location)
    {
        if (_session.Worker is null)
            return;

        if (ShiftOrchestrator.TrySelectChestDepositStandTile(chestDest.Ref.Tile, location, _session.Worker, out var standTile))
        {
            _nav.StartNavigation(standTile, location, _session.Worker);
            return;
        }

        MarkTripUndelivered(trip);
        _current = null;
    }

    // Safety warp for failure paths (undelivered trip, chest unreachable): the normal success path
    // walks out via the DepositExit travel, which leaves the worker already on the farm here.
    private void CompleteTripLocation(DepositTrip trip)
    {
        if (_session.Worker is null ||
            (_session.Worker.currentLocation ?? _session.CurrentLocation) == Game1.getFarm() ||
            trip.Destination is not ChestDestination { Ref.LocationName: not "Farm" } chestDest)
            return;

        if (ModEntry.ExpansionCompat is { } compat &&
            compat.IsExpansionDepositLocation(chestDest.Ref.LocationName))
            return;

        if (_buildingNavigator.TryResolveDoorTile(chestDest.Ref.LocationName, out var outdoorDoor, out _))
        {
            var farm = Game1.getFarm();
            _nav.WarpWorker(_session.Worker, _session.Worker.currentLocation ?? farm, farm, outdoorDoor);
            _session.CurrentLocation = farm;
        }
    }

    /// <summary>
    /// Sleep/stop settlement: routes the buffered-but-undeposited items plus any in-flight and
    /// queued trips through their safe final destination (shipping bin or automatic overflow).
    /// </summary>
    public void AppendUndeliveredToOverflow()
    {
        foreach (var b in _session.Ctx.Buffer.TakeAll())
        {
            var stack = new RoutedItemStack(b.QualifiedItemId, b.Quantity, b.SourceTask, b.Provenance, b.Quality, b.FlavorId);
            if (DepositPlanner.ResolveUndelivered(ShiftOrchestrator.ResolveAssignedDestination(b.SourceTask, _session.Ctx.TaskDestinations))
                == UndeliveredDepositResolution.ShippingBin)
                DepositIntoShippingBin(stack, animateWhenPlayerHere: false);
            else
                _session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NotDelivered));
        }

        if (_current is not null)
        {
            RouteUndeliveredTripStacks(
                _current,
                _current.Items.Skip(Math.Clamp(_stackIndex, 0, _current.Items.Count)));
            _current = null;
        }

        while (_trips.Count > 0)
        {
            var trip = _trips.Dequeue();
            RouteUndeliveredTripStacks(trip, trip.Items);
        }
    }

    private void RouteUndeliveredTripStacks(DepositTrip trip, IEnumerable<RoutedItemStack> stacks)
    {
        if (DepositPlanner.ResolveUndelivered(trip.Destination) == UndeliveredDepositResolution.ShippingBin)
        {
            foreach (var stack in stacks)
                DepositIntoShippingBin(stack, animateWhenPlayerHere: false);
            return;
        }

        foreach (var stack in stacks)
            _session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NotDelivered));
    }

    private int FacingTowardDestination()
    {
        if (_current is null || _session.Worker is null)
            return _session.Worker?.FacingDirection ?? 2;
        return ShiftOrchestrator.FacingToward(_session.Worker.TilePoint, _current.Tile, _session.Worker.FacingDirection);
    }
}
