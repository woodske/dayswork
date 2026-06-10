using Dayswork.Core.Crops;
using Dayswork.Core.Capabilities;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator
{
    private readonly ManagedCropFieldReader _cropFieldReader = new();
    private readonly CropShiftPlanner _cropShiftPlanner = new();
    private readonly PlantingViabilityCalculator _viability = new();
    private readonly CabinChestService _cabinChests = new();
    private readonly ShopStockReader _shopStockReader = new();
    private readonly ShopPurchaseService _shopPurchaseService = new();
    private readonly ShiftSupplyAggregator _shiftSupplyAggregator = new();
    private readonly PurchaseAffordabilityCalculator _purchaseAffordability = new();

    // Managed-crop batch execution: a single ordered queue of per-tile beats produced by the
    // pure CropShiftPlanner (harvest → clear → till → fertilize → seed → water), executed one beat
    // at a time through the existing tick/intent loop. The queue and its state live on the session.
    //
    // Re-plan support: the managed plan is snapshotted from the live field, so a tile that is debris
    // at snapshot only gets a ClearDebris action this pass. Once the queue drains we re-read the field
    // and re-plan, so newly-cleared tiles get tilled/planted on a following pass (clear -> till ->
    // fertilize -> seed -> water across passes). Bounded by a pass cap + a no-progress signature
    // check so un-clearable debris cannot loop forever.
    private const int MaxManagedReplans = 8;

    // Plan-level toggles, fixed at their default-ON behavior for now; a configurable OFF switch
    // (GMCM/per-plan) is deferred to a follow-up.
    private const bool _clearDebrisBeforeTilling = true;
    private const bool _clearDeadPlants = true;

    private void ResetManagedCropState()
    {
        if (_session is { } s)
        {
            s.ManagedActions.Clear();
            s.CurrentManagedAction = null;
            s.ManagedActive = false;
            s.ManagedAssignments = new();
            s.ManagedReplanCount = 0;
            s.LastManagedSignature = string.Empty;
            s.ManagedBatchLocationName = "Farm";
        }

        _session?.Shopping.ResetState();
    }

    private static bool IsManagedCropBatch(WorkBatch batch) => batch.Kind == BatchKind.ManagedCrops;

    private void BeginManagedCropBatch(WorkBatch batch)
    {
        if (_session is null || Session.Worker is null)
            return;

        ResetManagedCropState();

        Session.ManagedBatchLocationName = batch.LocationName;
        var activeLocation = ResolveManagedBatchLocation(batch.LocationName);
        if (activeLocation is null)
        {
            DevLog.Log($"[Dayswork][managed-crops] skipped batch={batch.LocationName} reason=location_unavailable.", LogLevel.Warn);
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.CurrentLocation = activeLocation;

        Session.ManagedAssignments = (Session.Ctx.WorkScopes.ManagedCrops?.Assignments ?? Array.Empty<CropZoneAssignment>())
            .Where(assignment => string.Equals(assignment.Zone.LocationName, batch.LocationName, StringComparison.Ordinal))
            .ToList();

        if (Session.ManagedAssignments.Count == 0)
        {
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        var actions = BuildManagedActions(logDetail: true);
        foreach (var action in actions)
            Session.ManagedActions.Enqueue(action);
        Session.LastManagedSignature = Signature(actions);

        DevLog.Log($"[Dayswork][managed-crops] batch={batch.LocationName} zones={Session.ManagedAssignments.Count} actions={Session.ManagedActions.Count}.", LogLevel.Info);

        Session.ManagedActive = true;
        StartNextManagedAction();
    }

    /// <summary>
    /// Re-reads the live field + input-chest supply and produces the toggle-filtered, ordered managed
    /// action list for the current batch's assignments. Pure inputs aside from the live world read; no
    /// mutation. Used both for the initial plan and each re-plan pass.
    /// </summary>
    private List<TileAction> BuildManagedActions(bool logDetail)
    {
        var location = Session.CurrentLocation ?? ResolveManagedBatchLocation(Session.ManagedBatchLocationName) ?? Game1.getFarm();
        var date = CurrentManagedGameDate();
        var isFestival = Utility.isFestivalDay(date.Day, Game1.season);
        var inputChest = TryGetInputChest();
        var supply = ReadSupply(inputChest);
        var fieldState = _cropFieldReader.Read(
            location,
            date,
            Session.ManagedAssignments,
            IsCurrentManagedBatchSeasonAgnostic());

        if (logDetail)
            DevLog.Log(
                $"[Dayswork][managed-crops] field date={date.Day}/{date.Season} tiles={fieldState.Tiles.Count} " +
                $"supply=[{string.Join(", ", supply.Items.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}] festival={isFestival} inputChest={(inputChest is null ? "null" : "ok")}.",
                LogLevel.Info);

        var actions = new List<TileAction>();
        foreach (var assignment in Session.ManagedAssignments)
        {
            var plan = _cropShiftPlanner.Plan(
                assignment,
                fieldState,
                supply,
                stockSnapshots: null,
                isFestivalDay: isFestival,
                storePreferenceOverride: ModEntry.PreferredCropStore);
            foreach (var action in plan.AllActions)
            {
                // Honor the plan-level toggles: skip debris/dead-plant clearing when disabled
                //. A skipped debris tile is simply not tilled/planted this shift.
                if (action.Kind == ManagedCropActionKind.ClearDebris && !ShouldClearDebrisTile(action.Tile, location))
                    continue;
                actions.Add(action);
            }

            if (logDetail)
            {
                DevLog.Log(
                    $"[Dayswork][managed-crops] zone ({assignment.Zone.TopLeft.X},{assignment.Zone.TopLeft.Y})-({assignment.Zone.BottomRight.X},{assignment.Zone.BottomRight.Y}) " +
                    $"choice={(assignment.Choices.FirstOrDefault()?.Crop.SeedItemId ?? "none")} requiresFert={assignment.Choices.FirstOrDefault()?.Crop.RequiresFertilizer} " +
                    $"independent={plan.SupplyIndependentActions.Count} dependent={plan.SupplyDependentActions.Count}.",
                    LogLevel.Info);
                NotifyFertilizerUnavailable(assignment, fieldState, supply, plan);
                NotifyCropNotViable(assignment, fieldState);
            }
        }

        return actions;
    }

    private void NotifyCropNotViable(CropZoneAssignment assignment, FieldState fieldState)
    {
        if (fieldState.IsSeasonAgnosticLocation)
            return;

        var choice = assignment.Mode == CropAssignmentMode.SeasonAgnostic
            ? assignment.Choices.FirstOrDefault()
            : assignment.Choices.FirstOrDefault(c => c.Season == fieldState.Date.Season);
        if (choice is null)
            return;

        // Only meaningful when the zone has empty space the player intended to plant.
        var zone = assignment.Zone;
        var hasOpenTiles = fieldState.Tiles.Any(t =>
            !t.HasCrop
            && t.Tile.X >= zone.TopLeft.X && t.Tile.X <= zone.BottomRight.X
            && t.Tile.Y >= zone.TopLeft.Y && t.Tile.Y <= zone.BottomRight.Y);
        if (!hasOpenTiles)
            return;

        if (!_viability.IsPlantingViable(fieldState, choice.Crop, choice.Crop.RequiresFertilizer))
        {
            var cropName = ResolveCropDisplayName(choice.Crop);
            DevLog.Log(
                $"[Dayswork][managed-crops] zone not viable — {cropName} ({choice.Crop.SeedItemId}) can't mature before season end; skipping till/plant.",
                LogLevel.Info);
            CropHudNotifier.CropWontGrowInTime(cropName);
        }
    }

    private static string ResolveCropDisplayName(CropDescriptor crop)
    {
        // Prefer the harvested crop's display name (e.g. "Cucumber"); fall back to the seed, then id.
        foreach (var id in new[] { crop.CropItemId, crop.SeedItemId })
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            try
            {
                var qualified = ItemRegistry.QualifyItemId(id) ?? id;
                var data = ItemRegistry.GetDataOrErrorItem(qualified);
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;
            }
            catch
            {
                // fall through to the next id / final fallback
            }
        }

        return crop.SeedItemId;
    }

    /// <summary>
    /// When the action queue drains, re-read the field and re-plan so tiles that were debris at the
    /// last snapshot (now cleared) get tilled/planted. Returns false when the pass cap is hit, no
    /// actions remain, or the plan is unchanged from the previous pass (no progress — e.g. only
    /// un-clearable debris left), in which case the batch completes.
    /// </summary>
    private bool TryReplanManagedBatch()
    {
        if (_session is null || Session.ManagedReplanCount >= MaxManagedReplans)
            return false;

        var actions = BuildManagedActions(logDetail: false);
        if (actions.Count == 0)
            return false;

        var signature = Signature(actions);
        if (signature == Session.LastManagedSignature)
            return false;

        Session.LastManagedSignature = signature;
        Session.ManagedReplanCount++;
        foreach (var action in actions)
            Session.ManagedActions.Enqueue(action);

        DevLog.Log($"[Dayswork][managed-crops] re-plan pass={Session.ManagedReplanCount} actions={actions.Count}.", LogLevel.Info);
        return true;
    }

    private static string Signature(IReadOnlyList<TileAction> actions) =>
        string.Join(";", actions
            .Select(a => $"{a.LocationName}|{a.Tile.X}|{a.Tile.Y}|{a.Kind}|{a.ItemId}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private void NotifyFertilizerUnavailable(
        CropZoneAssignment assignment,
        FieldState fieldState,
        SupplyInventory supply,
        ManagedCropShiftPlan plan)
    {
        var choice = assignment.Mode == CropAssignmentMode.SeasonAgnostic
            ? assignment.Choices.FirstOrDefault()
            : assignment.Choices.FirstOrDefault(c => c.Season == fieldState.Date.Season);

        if (choice is null || !choice.Crop.RequiresFertilizer)
            return;

        // The zone configures a fertilizer but none is on hand (no shopping this unit), so the
        // atomicity gate planted nothing. Fire one notice for the zone.
        var hasFertilizer = supply.QuantityOf(choice.Crop.FertilizerItemId!) > 0;
        var hasSeedCandidates = plan.SupplyDependentActions.Count > 0;
        if (!hasFertilizer && !hasSeedCandidates)
            CropHudNotifier.FertilizerUnavailable();
    }

    private void StartNextManagedAction()
    {
        if (_session is null || Session.Worker is null)
            return;

        Session.Stuck.Reset();
        Session.ActionPending = false;

        if (ShouldWrapUpBeforeNextUnit())
        {
            if (Session.Shopping.TryStartIfNeeded(wrapAfterReturn: true))
                return;

            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        var location = Session.CurrentLocation ?? ResolveManagedBatchLocation(Session.ManagedBatchLocationName) ?? Game1.getFarm();

        while (Session.ManagedActions.Count > 0)
        {
            var action = Session.ManagedActions.Dequeue();
            if (!IsManagedActionApplicable(action, location))
                continue;

            Session.CurrentManagedAction = action;
            var navTile = ResolveManagedNavTile(action, location);
            Session.PendingNavTile = navTile;
            Session.PendingTaskTile = action.Tile;
            EnsureWorkingIntent(new IntentMoveToTile(navTile));
            _nav.StartNavigation(navTile, location, Session.Worker);
            return;
        }

        // Queue drained: re-plan so newly-cleared (formerly-debris) tiles get tilled/planted on a
        // following pass. Completes when no further progress is possible.
        if (TryReplanManagedBatch())
        {
            StartNextManagedAction();
            return;
        }

        if (Session.Shopping.TryStartIfNeeded(wrapAfterReturn: false))
            return;

        CompleteManagedCropBatch();
    }

    internal void CompleteManagedCropBatch()
    {
        if (_session is null)
            return;

        var completedLocationName = Session.ManagedBatchLocationName;
        ReturnLeftoverSuppliesNoop();
        ResetManagedCropState();

        // Walk out of a non-farm work location through its door, like every other batch exit.
        if (TryStartManagedBatchExitTravel(completedLocationName))
            return;

        Session.Ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    /// <summary>
    /// Starts the walk-to-door exit back to the farm after a managed batch in a non-farm location.
    /// Returns false when the worker is already on the farm (or the location can't be resolved) and
    /// the caller should advance to the next batch synchronously.
    /// </summary>
    private bool TryStartManagedBatchExitTravel(string locationName)
    {
        if (_session is null || Session.Worker is null)
            return false;

        var farm = Game1.getFarm();
        if (string.Equals(locationName, "Farm", StringComparison.Ordinal))
        {
            Session.CurrentLocation = farm;
            return false;
        }

        var current = Session.Worker.currentLocation ?? Session.CurrentLocation;
        if (current is null || current == farm)
        {
            Session.CurrentLocation = farm;
            return false;
        }

        // Expansion greenhouse: hop home along the validated route.
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetExpansionLocationDescriptor(locationName, out var descriptor) &&
            descriptor.Role == ExpansionLocationRole.GreenhouseWork)
        {
            Session.Ctx.CurrentBatchIndex++;
            if (!TryStartExpansionTravel(
                    current.NameOrUniqueName,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    TravelFailurePolicy.WarpToDestination,
                    TravelPurpose.WorkExit))
            {
                WarpExpansionWorkerToFarm();
                BeginCurrentBatch();
            }

            return true;
        }

        // Vanilla building interior: walk to the interior door, warp out at the outdoor door.
        var farmArrival = _buildingNavigator.TryResolveDoorTile(locationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : Session.FarmExitTile;
        Session.Ctx.CurrentBatchIndex++;
        StartTravel(BuildBuildingExitPlan(current, farmArrival), TravelPurpose.WorkExit);
        return true;
    }

    /// <summary>
    /// Walks the worker back into the managed batch's building after a shopping trip, then resumes
    /// planting via the ManagedReentry travel completion. Returns false when re-entry is impossible
    /// and the caller should complete the batch instead.
    /// </summary>
    internal bool TryStartManagedReentryTravel()
    {
        if (Session.Worker is null)
            return false;

        var farm = Game1.getFarm();
        var current = Session.Worker.currentLocation ?? Session.CurrentLocation ?? farm;
        var target = ResolveManagedBatchLocation(Session.ManagedBatchLocationName);
        if (target is null)
        {
            DevLog.Log(
                $"[Dayswork][managed-crops][shopping] re-entry skipped location={Session.ManagedBatchLocationName} reason=location_unavailable.",
                LogLevel.Warn);
            return false;
        }

        if (SameLocation(current, target))
        {
            Session.CurrentLocation = target;
            ResumeManagedBatchAfterShopping();
            return true;
        }

        // Expansion greenhouse: hop back in along the validated route.
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetExpansionLocationDescriptor(Session.ManagedBatchLocationName, out var descriptor) &&
            descriptor.Role == ExpansionLocationRole.GreenhouseWork)
        {
            if (compat.TryValidateRoute(
                    farm,
                    current.NameOrUniqueName,
                    Session.ManagedBatchLocationName,
                    ExpansionRoutePurpose.WorkEntry,
                    out var route,
                    out var failure))
            {
                StartTravel(
                    BuildExpansionPlan(route, TravelFailurePolicy.WarpToDestination),
                    TravelPurpose.ManagedReentry);
                return true;
            }

            LogExpansionRouteFailure(failure);
            return false;
        }

        // Vanilla building: walk to its outdoor door, warp inside.
        if (TryBuildBuildingEntryPlan(
                Session.ManagedBatchLocationName,
                TravelFailurePolicy.WarpToDestination,
                out var plan))
        {
            StartTravel(plan, TravelPurpose.ManagedReentry);
            return true;
        }

        return false;
    }

    /// <summary>Re-plan and resume planting once the worker is back in the managed batch's location.</summary>
    internal void ResumeManagedBatchAfterShopping()
    {
        Session.ManagedReplanCount = 0;
        var actions = BuildManagedActions(logDetail: true);
        foreach (var action in actions)
            Session.ManagedActions.Enqueue(action);
        Session.LastManagedSignature = Signature(actions);

        if (Session.ManagedActions.Count == 0)
        {
            CompleteManagedCropBatch();
            return;
        }

        StartNextManagedAction();
    }

    internal static void NotifyFallbackStoreIfUsed(ShiftPurchaseManifest manifest)
    {
        var preferred = ModEntry.PreferredCropStore switch
        {
            StorePreference.Pierre => Store.Pierre,
            StorePreference.Joja => Store.Joja,
            _ => (Store?)null,
        };
        if (preferred is null)
            return;

        if (manifest.Groups.Any(group => group.Store != preferred.Value))
            CropHudNotifier.UsingFallbackStore(preferred.Value == Store.Pierre ? "Pierre's" : "JojaMart");
    }

    private bool ShouldClearDebrisTile(TileCoord tile, GameLocation location)
    {
        var vec = new Vector2(tile.X, tile.Y);
        var isDeadCrop = location.terrainFeatures.TryGetValue(vec, out var tf)
            && tf is HoeDirt dirt && dirt.crop is not null && dirt.crop.dead.Value;
        return isDeadCrop ? _clearDeadPlants : _clearDebrisBeforeTilling;
    }

    // Visible shopping deposits bought supplies into the input chest before replanning, so there is
    // no separate carried-supply settlement when the managed-crop batch completes normally.
    private void ReturnLeftoverSuppliesNoop() { }

    private void HandleManagedCropAction(IntentPerformManagedCropAction intent, GameLocation location)
    {
        if (_session is null || Session.Worker is null)
            return;

        var action = intent.Action;

        if (!Session.ActionPending)
        {
            var debrisTool = WorkerTool.None;
            if (action.Kind == ManagedCropActionKind.ClearDebris)
                ResolveDebrisClearing(action.Tile, location, out debrisTool, out var capable);
            var tool = ManagedCropActionMap.Tool(action.Kind, debrisTool);

            if (ManagedCropActionMap.IsToolGated(action.Kind) && !HasManagedCapability(action, location, debrisTool))
            {
                CropHudNotifier.ToolSkip(action.Kind);
                StartNextManagedAction();
                return;
            }

            if (!IsManagedActionApplicable(action, location))
            {
                StartNextManagedAction();
                return;
            }

            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(tool, FacingToward(Session.Worker.TilePoint, action.Tile, Session.Worker.FacingDirection));
            ApplyManagedActionGuarded(action, debrisTool, location);
            SpendStaminaForBeat(ManagedCropActionMap.EnergyKind(action.Kind, debrisTool));
            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        Session.ActionPending = false;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            unitResolved: true,
            Session.Ctx.EnergyState,
            HasBoundaryStopRequested());

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            if (Session.Shopping.TryStartIfNeeded(wrapAfterReturn: true))
                return;

            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        StartNextManagedAction();
    }

    private bool HasManagedCapability(TileAction action, GameLocation location, WorkerTool debrisTool)
    {
        if (action.Kind != ManagedCropActionKind.ClearDebris)
            return true; // till/water use vanilla starter tools, effectively always present

        ResolveDebrisClearing(action.Tile, location, out _, out var capable);
        return capable;
    }

    private bool IsManagedActionApplicable(TileAction action, GameLocation location)
    {
        var vec = new Vector2(action.Tile.X, action.Tile.Y);
        location.terrainFeatures.TryGetValue(vec, out var tf);
        var dirt = tf as HoeDirt;

        return action.Kind switch
        {
            ManagedCropActionKind.Harvest =>
                dirt?.crop is not null && !dirt.crop.dead.Value && dirt.readyForHarvest(),

            ManagedCropActionKind.ClearDebris =>
                location.objects.ContainsKey(vec)
                || (tf is not null && tf is not HoeDirt)
                || (dirt?.crop is not null && dirt.crop.dead.Value)
                || ObjectTargetClassifier.FindResourceClumpAt(vec, location) is not null,

            ManagedCropActionKind.Till =>
                dirt is null
                && !location.objects.ContainsKey(vec)
                && tf is null
                && location.doesTileHaveProperty(action.Tile.X, action.Tile.Y, "Diggable", "Back") is not null,

            ManagedCropActionKind.Fertilize =>
                dirt is not null
                && (string.IsNullOrEmpty(dirt.fertilizer.Value) || dirt.fertilizer.Value == "0")
                && action.ItemId is not null
                && SupplyOnHand(action.ItemId),

            ManagedCropActionKind.PlantSeed =>
                dirt is not null
                && dirt.crop is null
                && action.ItemId is not null
                && SupplyOnHand(action.ItemId),

            ManagedCropActionKind.Water =>
                dirt is not null && dirt.state.Value != HoeDirt.watered,

            _ => false,
        };
    }

    private TileCoord ResolveManagedNavTile(TileAction action, GameLocation location)
    {
        var tilePoint = new Point(action.Tile.X, action.Tile.Y);
        if (WorkerMovementDriver.IsTilePassableForWorker(tilePoint, location))
            return action.Tile;

        TileCoord[] neighbours =
        {
            new(action.Tile.X - 1, action.Tile.Y),
            new(action.Tile.X + 1, action.Tile.Y),
            new(action.Tile.X, action.Tile.Y - 1),
            new(action.Tile.X, action.Tile.Y + 1),
        };

        foreach (var n in neighbours)
        {
            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(n.X, n.Y), location))
                return n;
        }

        return action.Tile;
    }

    // ── World mutations ──────────────────────────────────────────────────────

    private void ApplyManagedActionGuarded(TileAction action, WorkerTool debrisTool, GameLocation location)
    {
        // Mirror InvokeTaskActionGuarded: some vanilla crop callbacks mutate Game1.player and enqueue
        // HUD pickup messages even though the worker is acting. Snapshot/restore and trim.
        var playerState = new Game1WorkerActionPlayerState(Game1.player);
        var savedState = WorkerActionPlayerStateSnapshot.Capture(playerState);
        var hudCountBefore = Game1.hudMessages.Count;

        try
        {
            ApplyManagedAction(action, debrisTool, location);
        }
        finally
        {
            savedState.Restore(playerState);
            while (Game1.hudMessages.Count > hudCountBefore)
                Game1.hudMessages.RemoveAt(Game1.hudMessages.Count - 1);
        }
    }

    private void ApplyManagedAction(TileAction action, WorkerTool debrisTool, GameLocation location)
    {
        Session.PendingOutputProvenance =
            action.Kind == ManagedCropActionKind.Harvest && action.OutputProvenance is not null
                ? action.OutputProvenance
                : OutputScopeProvenance.Outdoor();
        var vec = new Vector2(action.Tile.X, action.Tile.Y);

        switch (action.Kind)
        {
            case ManagedCropActionKind.Harvest:
                Session.PendingTask = TaskKind.HarvestCrops;
                InvokeHarvest(action.Tile, location);
                break;

            case ManagedCropActionKind.ClearDebris:
                InvokeManagedClearDebris(action.Tile, location);
                break;

            case ManagedCropActionKind.Till:
                location.makeHoeDirt(vec);
                break;

            case ManagedCropActionKind.Fertilize:
                ApplyManagedFertilize(action, location, vec);
                break;

            case ManagedCropActionKind.PlantSeed:
                ApplyManagedSeed(action, location, vec);
                break;

            case ManagedCropActionKind.Water:
                InvokeWater(action.Tile, location);
                break;
        }
    }

    private void ApplyManagedFertilize(TileAction action, GameLocation location, Vector2 vec)
    {
        if (action.ItemId is null)
            return;
        if (!location.terrainFeatures.TryGetValue(vec, out var tf) || tf is not HoeDirt dirt)
        {
            DevLog.Log($"[Dayswork][managed-crops] fertilize skipped — no HoeDirt at ({action.Tile.X},{action.Tile.Y}).", LogLevel.Info);
            return;
        }

        var who = CreateWorkerActionFarmer(action.Tile, location);
        var applied = dirt.plant(StripQualifier(action.ItemId), who, isFertilizer: true);
        DevLog.Log($"[Dayswork][managed-crops] fertilize tile=({action.Tile.X},{action.Tile.Y}) id={StripQualifier(action.ItemId)} applied={applied}.", LogLevel.Info);
        if (applied)
            ConsumeSupply(action.ItemId, 1);
    }

    private void ApplyManagedSeed(TileAction action, GameLocation location, Vector2 vec)
    {
        if (action.ItemId is null)
            return;
        if (!location.terrainFeatures.TryGetValue(vec, out var tf) || tf is not HoeDirt dirt)
        {
            DevLog.Log($"[Dayswork][managed-crops] seed skipped — no HoeDirt at ({action.Tile.X},{action.Tile.Y}).", LogLevel.Info);
            return;
        }

        var who = CreateWorkerActionFarmer(action.Tile, location);
        var unqualified = StripQualifier(action.ItemId);
        var planted = dirt.plant(unqualified, who, isFertilizer: false);
        DevLog.Log(
            $"[Dayswork][managed-crops] seed tile=({action.Tile.X},{action.Tile.Y}) id={unqualified} planted={planted} season={Game1.currentSeason} dirtSeason={location.GetSeason()}.",
            LogLevel.Info);
        if (planted)
            ConsumeSupply(action.ItemId, 1);
    }

    private void InvokeManagedClearDebris(TileCoord tile, GameLocation location)
    {
        var vec = new Vector2(tile.X, tile.Y);

        // Dead crop on tilled soil: free the tile for re-till/replant.
        if (location.terrainFeatures.TryGetValue(vec, out var tf) && tf is HoeDirt dirt
            && dirt.crop is not null && dirt.crop.dead.Value)
        {
            dirt.destroyCrop(false);
            return;
        }

        if (ObjectTargetClassifier.ClassifyAxe(vec, location) is not null
            || (location.objects.TryGetValue(vec, out var twig) && twig.Name == "Twig"))
        {
            Session.PendingTask = TaskKind.CutTrees;
            InvokeCutTree(tile, location);
            return;
        }

        if (ObjectTargetClassifier.ClassifyPick(vec, location) is not null)
        {
            Session.PendingTask = TaskKind.ClearRocks;
            InvokeClearRock(tile, location);
            return;
        }

        if (location.objects.TryGetValue(vec, out var obj) && obj.IsWeeds())
        {
            Session.PendingTask = TaskKind.ClearWeeds;
            InvokeClearWeed(tile, location);
            return;
        }

        if (tf is Grass)
        {
            Session.PendingTask = TaskKind.ClearGrass;
            InvokeClearGrass(tile, location);
        }
    }

    private void ResolveDebrisClearing(TileCoord tile, GameLocation location, out WorkerTool tool, out bool capable)
    {
        var vec = new Vector2(tile.X, tile.Y);

        if (location.terrainFeatures.TryGetValue(vec, out var tf) && tf is HoeDirt dirt
            && dirt.crop is not null && dirt.crop.dead.Value)
        {
            tool = WorkerTool.Scythe;
            capable = true;
            return;
        }

        if (ObjectTargetClassifier.ClassifyAxe(vec, location) is { } axeTarget)
        {
            tool = WorkerTool.Axe;
            capable = CapabilityMatrix.CanChop(Session.Ctx.ToolSnapshot.AxeLevel, axeTarget);
            return;
        }

        if (ObjectTargetClassifier.ClassifyPick(vec, location) is { } pickTarget)
        {
            tool = WorkerTool.Pickaxe;
            capable = CapabilityMatrix.CanBreak(Session.Ctx.ToolSnapshot.PickaxeLevel, pickTarget);
            return;
        }

        if (location.objects.TryGetValue(vec, out var obj) && obj.Name == "Twig")
        {
            tool = WorkerTool.Axe;
            capable = true;
            return;
        }

        // Weeds, grass, or anything else clearable with the scythe.
        tool = WorkerTool.Scythe;
        capable = true;
    }

    // ── Input-chest supply ───────────────────────────────────────────────────

    internal Chest? TryGetInputChest()
    {
        var farm = Game1.getFarm();
        foreach (var building in farm.buildings)
        {
            if (!string.Equals(building.buildingType.Value, HiringBuilding.BuildingType, StringComparison.Ordinal))
                continue;

            return _cabinChests.EnsureInputChest(building);
        }

        return null;
    }

    internal static SupplyInventory ReadSupply(Chest? chest)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (chest is not null)
        {
            foreach (var item in chest.Items)
            {
                if (item is null)
                    continue;
                var id = StripQualifier(item.QualifiedItemId);
                counts.TryGetValue(id, out var existing);
                counts[id] = existing + Math.Max(1, item.Stack);
            }
        }

        return new SupplyInventory(counts);
    }

    private bool SupplyOnHand(string itemId)
    {
        var chest = TryGetInputChest();
        if (chest is null)
            return false;

        var target = StripQualifier(itemId);
        foreach (var item in chest.Items)
        {
            if (item is not null && StripQualifier(item.QualifiedItemId) == target && item.Stack > 0)
                return true;
        }

        return false;
    }

    private bool ConsumeSupply(string itemId, int count)
    {
        var chest = TryGetInputChest();
        if (chest is null)
            return false;

        var target = StripQualifier(itemId);
        var remaining = count;
        for (var i = 0; i < chest.Items.Count && remaining > 0; i++)
        {
            var item = chest.Items[i];
            if (item is null || StripQualifier(item.QualifiedItemId) != target)
                continue;

            var take = Math.Min(item.Stack, remaining);
            item.Stack -= take;
            remaining -= take;
            if (item.Stack <= 0)
                chest.Items[i] = null;
        }

        if (remaining > 0)
            return false;

        chest.clearNulls();
        return true;
    }

    private static string StripQualifier(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;
        var close = id.IndexOf(')');
        return id.StartsWith("(", StringComparison.Ordinal) && close >= 0 ? id[(close + 1)..] : id;
    }

    internal static GameDate CurrentManagedGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }

    internal GameLocation? ResolveManagedBatchLocation(string locationName)
    {
        if (string.Equals(locationName, "Farm", StringComparison.Ordinal))
            return Game1.getFarm();

        return Game1.getLocationFromName(locationName);
    }

    internal static string LocationKey(GameLocation location) =>
        location.NameOrUniqueName ?? location.Name;

    internal static bool SameLocation(GameLocation left, GameLocation right) =>
        string.Equals(LocationKey(left), LocationKey(right), StringComparison.OrdinalIgnoreCase);

    internal bool IsCurrentManagedBatchSeasonAgnostic() =>
        !string.Equals(Session.ManagedBatchLocationName, "Farm", StringComparison.Ordinal)
        || Session.ManagedAssignments.Any(assignment => assignment.Mode == CropAssignmentMode.SeasonAgnostic);

}
