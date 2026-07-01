namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class CropShiftPlannerTests
{
    [Fact]
    public void Plan_OrdersTileActionsByDependency()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", "fert.basic", 4, 3, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, false, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int>
        {
            ["seed.parsnip"] = 1,
            ["fert.basic"] = 1,
        });

        var result = planner.Plan(assignment, field, inventory);

        Assert.Equal(
            new[]
            {
                ManagedCropActionKind.Till,
                ManagedCropActionKind.Fertilize,
                ManagedCropActionKind.PlantSeed,
                ManagedCropActionKind.Water,
            },
            result.AllActions.Select(action => action.Kind));
    }

    [Fact]
    public void Plan_FertilizerRequiredWithOnlySeed_DoesNotPlantSeed()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", "fert.basic", 4, 3, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, true, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 1 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.DoesNotContain(result.AllActions, action => action.Kind == ManagedCropActionKind.PlantSeed);
    }

    [Fact]
    public void Plan_CropNotViableBeforeSeasonEnd_DoesNotTillOrPlantBareTile()
    {
        var planner = new CropShiftPlanner();
        // 20-day crop planted on day 20 of a 28-day season cannot mature in time.
        var crop = new CropDescriptor("crop.x", "seed.x", null, 20, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(20, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, false, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.x"] = 1 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.DoesNotContain(result.AllActions, action => action.Kind == ManagedCropActionKind.Till);
        Assert.DoesNotContain(result.AllActions, action => action.Kind == ManagedCropActionKind.PlantSeed);
    }

    [Fact]
    public void Plan_SeasonAgnosticLocation_StillTillsAndPlantsRegardlessOfDate()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.x", "seed.x", null, 20, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Greenhouse", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.SeasonAgnostic,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Greenhouse",
            new GameDate(28, Season.Winter, 1),
            true,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, false, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.x"] = 1 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.Contains(result.AllActions, action => action.Kind == ManagedCropActionKind.Till);
        Assert.Contains(result.AllActions, action => action.Kind == ManagedCropActionKind.PlantSeed);
    }

    // Regression: two fert-requiring zones share an input chest that has seeds for both but
    // fertilizer for only one. The orchestrator decrements a working supply between assignments so
    // the second zone sees no fertilizer and plants nothing — a seed is never planted un-fertilized.
    // This mirrors ShiftOrchestrator.BuildManagedActions' per-assignment consume loop.
    [Fact]
    public void Plan_TwoFertZonesSharingShortFertilizer_NeverPlantsMoreSeedsThanFertilizer()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", "fert.basic", 4, 3, null, new[] { Season.Spring });

        CropZoneAssignment ZoneAt(int x) => new(
            new Zone("Farm", new TileCoord(x, 0), new TileCoord(x, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });

        var assignments = new[] { ZoneAt(0), ZoneAt(1) };
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[]
            {
                new TileState(new TileCoord(0, 0), false, false, false, false, false, false),
                new TileState(new TileCoord(1, 0), false, false, false, false, false, false),
            });

        // Seeds for both tiles, but only one unit of fertilizer to share between them.
        var working = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["seed.parsnip"] = 2,
            ["fert.basic"] = 1,
        };

        var totalPlanted = 0;
        foreach (var assignment in assignments)
        {
            var plan = planner.Plan(assignment, field, new SupplyInventory(working));
            totalPlanted += plan.AllActions.Count(a => a.Kind == ManagedCropActionKind.PlantSeed);

            foreach (var action in plan.SupplyDependentActions)
            {
                if (action.ItemId is null)
                    continue;
                if (action.Kind != ManagedCropActionKind.PlantSeed && action.Kind != ManagedCropActionKind.Fertilize)
                    continue;
                if (working.TryGetValue(action.ItemId, out var have))
                {
                    if (have <= 1)
                        working.Remove(action.ItemId);
                    else
                        working[action.ItemId] = have - 1;
                }
            }
        }

        // Only one fertilizer was available, so only one seed may be planted.
        Assert.Equal(1, totalPlanted);
    }

    [Fact]
    public void Plan_DebrisAndBareTiles_RunFullFlowPerTile_BoundedBySeed()
    {
        // Zone (0,0)-(1,0): tile (0,0) has debris, tile (1,0) is bare untilled. Two seeds on hand,
        // so each plantable tile runs its whole flow before the next: (0,0) Clear→Till→Plant→Water,
        // then (1,0) Till→Plant→Water. Ground prep now lives in the supply-dependent sequence.
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[]
            {
                new TileState(new TileCoord(0, 0), false, false, HasDebris: true, false, false, false),
                new TileState(new TileCoord(1, 0), false, false, HasDebris: false, false, false, false),
            });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 2 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.Empty(result.SupplyIndependentActions);
        Assert.Equal(
            new[]
            {
                (new TileCoord(0, 0), ManagedCropActionKind.ClearDebris),
                (new TileCoord(0, 0), ManagedCropActionKind.Till),
                (new TileCoord(0, 0), ManagedCropActionKind.PlantSeed),
                (new TileCoord(0, 0), ManagedCropActionKind.Water),
                (new TileCoord(1, 0), ManagedCropActionKind.Till),
                (new TileCoord(1, 0), ManagedCropActionKind.PlantSeed),
                (new TileCoord(1, 0), ManagedCropActionKind.Water),
            },
            result.AllActions.Select(a => (a.Tile, a.Kind)));
    }

    [Fact]
    public void Plan_NoSeedOnHand_DoesNotClearOrTill()
    {
        // Same debris + bare zone, but the chest is empty: with no seed, the worker preps nothing —
        // no clear, no till, no plant. This is the whole-zone-tilling waste the change removes.
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[]
            {
                new TileState(new TileCoord(0, 0), false, false, HasDebris: true, false, false, false),
                new TileState(new TileCoord(1, 0), false, false, HasDebris: false, false, false, false),
            });
        var inventory = new SupplyInventory(new Dictionary<string, int>());

        var result = planner.Plan(assignment, field, inventory);

        Assert.DoesNotContain(result.AllActions, a => a.Kind == ManagedCropActionKind.ClearDebris);
        Assert.DoesNotContain(result.AllActions, a => a.Kind == ManagedCropActionKind.Till);
        Assert.DoesNotContain(result.AllActions, a => a.Kind == ManagedCropActionKind.PlantSeed);
    }

    // Regression: steady-state "no work" day — every tile in the zone holds a growing crop that is
    // already watered and not yet ready to harvest. Nothing to plant (no open tile), water, or
    // harvest, so the plan is empty even with seed on hand. This is the predicate
    // ManagedCropBatchHasReadyWork keys off to skip the daily walk into an idle managed location.
    [Fact]
    public void Plan_GrowingWateredUnripeCrop_ProducesEmptyPlan()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(2, Season.Spring, 1),
            false,
            new[]
            {
                // ReadyToHarvest: false, HasCrop: true, HasDebris: false, IsTilled: true,
                // HasFertilizer: false, IsWatered: true → nothing to do.
                new TileState(new TileCoord(0, 0), false, true, false, true, false, true),
            });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 5 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.Empty(result.SupplyIndependentActions);
        Assert.Empty(result.SupplyDependentActions);
        Assert.Empty(result.AllActions);
    }

    // Regression: an out-of-season managed group (a Seasonal assignment with no choice for the
    // current season) has no work regardless of open tiles or seed — the planner finds no crop to
    // plant and no live crop to maintain, so the plan is empty and the location is skipped.
    [Fact]
    public void Plan_NoChoiceForCurrentSeason_ProducesEmptyPlan()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Summer, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, false, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 5 });

        var result = planner.Plan(assignment, field, inventory);

        Assert.Empty(result.AllActions);
    }
}
