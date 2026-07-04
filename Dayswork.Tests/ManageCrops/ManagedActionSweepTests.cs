using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.ManageCrops;

public sealed class ManagedActionSweepTests
{
    private const string Location = "Farm";

    [Fact]
    public void Interleaved_zone_actions_merge_into_a_single_row_sweep()
    {
        // Two zones side by side on the same rows (zone A: x 0–1, zone B: x 2–3), appended in
        // zone order the way BuildManagedActions produces them. The sweep must cross the zone
        // boundary row by row instead of finishing zone A first.
        var zoneA = new[]
        {
            Water(0, 0), Water(1, 0),
            Water(0, 1), Water(1, 1),
        };
        var zoneB = new[]
        {
            Water(2, 0), Water(3, 0),
            Water(2, 1), Water(3, 1),
        };

        var ordered = ManagedActionSweep.Order(zoneA.Concat(zoneB).ToList());

        var tiles = ordered.Select(action => action.Tile).ToList();
        Assert.Equal(
            new[]
            {
                new TileCoord(0, 0), new TileCoord(1, 0), new TileCoord(2, 0), new TileCoord(3, 0),
                new TileCoord(3, 1), new TileCoord(2, 1), new TileCoord(1, 1), new TileCoord(0, 1),
            },
            tiles);
    }

    [Fact]
    public void Per_tile_chain_stays_in_action_rank_order()
    {
        // A planting chain plus a maintenance pair, deliberately shuffled: the sweep must restore
        // harvest → clear → till → fertilize → seed → water within each tile.
        var actions = new[]
        {
            new TileAction(Location, new TileCoord(0, 0), ManagedCropActionKind.Water),
            new TileAction(Location, new TileCoord(0, 0), ManagedCropActionKind.PlantSeed, "472"),
            new TileAction(Location, new TileCoord(0, 0), ManagedCropActionKind.Till),
            new TileAction(Location, new TileCoord(0, 0), ManagedCropActionKind.ClearDebris),
            new TileAction(Location, new TileCoord(0, 0), ManagedCropActionKind.Fertilize, "368"),
        };

        var ordered = ManagedActionSweep.Order(actions);

        Assert.Equal(
            new[]
            {
                ManagedCropActionKind.ClearDebris,
                ManagedCropActionKind.Till,
                ManagedCropActionKind.Fertilize,
                ManagedCropActionKind.PlantSeed,
                ManagedCropActionKind.Water,
            },
            ordered.Select(action => action.Kind));
    }

    [Fact]
    public void Harvest_precedes_water_on_the_same_tile()
    {
        var actions = new[]
        {
            new TileAction(Location, new TileCoord(4, 2), ManagedCropActionKind.Water),
            new TileAction(Location, new TileCoord(4, 2), ManagedCropActionKind.Harvest),
        };

        var ordered = ManagedActionSweep.Order(actions);

        Assert.Equal(ManagedCropActionKind.Harvest, ordered[0].Kind);
        Assert.Equal(ManagedCropActionKind.Water, ordered[1].Kind);
    }

    [Fact]
    public void Start_hint_orients_the_sweep_toward_the_worker()
    {
        var actions = new[]
        {
            Water(0, 0), Water(1, 0),
            Water(0, 5), Water(1, 5),
        };

        var ordered = ManagedActionSweep.Order(actions, start: new TileCoord(1, 8));

        // Worker enters from the south-east: bottom row first, right-to-left.
        Assert.Equal(new TileCoord(1, 5), ordered[0].Tile);
        Assert.Equal(new TileCoord(0, 5), ordered[1].Tile);
        Assert.Equal(new TileCoord(0, 0), ordered[2].Tile);
        Assert.Equal(new TileCoord(1, 0), ordered[3].Tile);
    }

    [Fact]
    public void Maintenance_and_planting_passes_collapse_into_one_visit_per_tile()
    {
        // Old behavior: harvest/water for all crop tiles, THEN plant chains for all empty tiles.
        // New behavior: strictly one contiguous run of actions per tile.
        var maintenance = new[] { Water(0, 0), Water(2, 0) };
        var planting = new[]
        {
            new TileAction(Location, new TileCoord(1, 0), ManagedCropActionKind.Till),
            new TileAction(Location, new TileCoord(1, 0), ManagedCropActionKind.PlantSeed, "472"),
            new TileAction(Location, new TileCoord(1, 0), ManagedCropActionKind.Water),
        };

        var ordered = ManagedActionSweep.Order(maintenance.Concat(planting).ToList());

        var visitedTiles = new List<TileCoord>();
        foreach (var action in ordered)
        {
            if (visitedTiles.Count == 0 || visitedTiles[^1] != action.Tile)
                visitedTiles.Add(action.Tile);
        }

        Assert.Equal(
            new[] { new TileCoord(0, 0), new TileCoord(1, 0), new TileCoord(2, 0) },
            visitedTiles);
    }

    private static TileAction Water(int x, int y) =>
        new(Location, new TileCoord(x, y), ManagedCropActionKind.Water);
}
