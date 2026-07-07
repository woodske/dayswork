using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Xunit;

namespace Dayswork.Tests.Inventory;

/// <summary>
/// Cross-location deposit trip ordering (architecture review #6). Uses a metric mirroring
/// <c>ShiftOrchestrator.DepositStopDistance</c> (same-location = Manhattan; cross-location =
/// hop*K + door-tile Manhattan) to prove the pure planner chains trips location-aware: farm chests
/// group, interior chests group, and there is no farm → interior → farm zig-zag.
/// </summary>
public sealed class CrossLocationDepositOrderingTests
{
    private const int K = 10_000;

    private static readonly IReadOnlyDictionary<string, TileCoord> Doors = new Dictionary<string, TileCoord>
    {
        ["Shed1"] = new TileCoord(10, 0),
        ["Shed2"] = new TileCoord(40, 0),
    };

    private static readonly DepositStop Bin = new("Farm", new TileCoord(71, 13));
    private static readonly DepositStop Start = new("Farm", new TileCoord(0, 0));

    private static int Manhattan(TileCoord a, TileCoord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static TileCoord FarmSide(DepositStop s) => s.LocationName == "Farm" ? s.Tile : Doors[s.LocationName];

    private static int Metric(DepositStop a, DepositStop b)
    {
        if (string.Equals(a.LocationName, b.LocationName, StringComparison.Ordinal))
            return Manhattan(a.Tile, b.Tile);

        var hops = a.LocationName != "Farm" && b.LocationName != "Farm" ? 2 : 1;
        return hops * K + Manhattan(FarmSide(a), FarmSide(b));
    }

    private static BufferedItem Item(string id, TaskKind task) =>
        new(id, 1, task, OutputScopeProvenance.Unknown, 0);

    private static DestinationKey Chest(string location, int x, int y) =>
        new ChestDestination(new ChestRef(location, new TileCoord(x, y)));

    private static IReadOnlyList<string> TripLocations(DepositPlan plan) =>
        plan.Trips.Select(t => ((ChestDestination)t.Destination).Ref.LocationName).ToList();

    [Fact]
    public void MixedFarmAndInterior_AllFarmStopsBeforeInterior_NoZigZag()
    {
        var snapshot = new List<BufferedItem>
        {
            Item("(O)1", TaskKind.CutTrees),
            Item("(O)2", TaskKind.ClearRocks),
            Item("(O)3", TaskKind.ClearWeeds),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CutTrees]   = Chest("Farm", 1, 0),
            [TaskKind.ClearRocks] = Chest("Farm", 2, 0),
            [TaskKind.ClearWeeds] = Chest("Shed1", 5, 5),
        };

        var plan = new DepositPlanner().Plan(snapshot, assignments, Bin, Start, Metric);

        // Both farm chests visited before entering the shed — the shed hop is paid once, last.
        Assert.Equal(new[] { "Farm", "Farm", "Shed1" }, TripLocations(plan));
    }

    [Fact]
    public void TwoChestsInSameInterior_AreVisitedConsecutively_NotInterleaved()
    {
        var snapshot = new List<BufferedItem>
        {
            Item("(O)1", TaskKind.CutTrees),
            Item("(O)2", TaskKind.ClearRocks),
            Item("(O)3", TaskKind.ClearWeeds),
            Item("(O)4", TaskKind.ClearGrass),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CutTrees]   = Chest("Farm", 1, 0),
            [TaskKind.ClearRocks] = Chest("Shed1", 5, 5),
            [TaskKind.ClearWeeds] = Chest("Shed1", 6, 5),
            [TaskKind.ClearGrass] = Chest("Shed2", 50, 50),
        };

        var plan = new DepositPlanner().Plan(snapshot, assignments, Bin, Start, Metric);

        var locs = TripLocations(plan);
        Assert.Equal("Farm", locs[0]);
        Assert.Equal("Shed1", locs[1]);
        Assert.Equal("Shed1", locs[2]); // grouped with its sibling, not split by Shed2
        Assert.Equal("Shed2", locs[3]);
    }

    [Fact]
    public void OrderingIsDeterministic_UnderPermutedInput()
    {
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CutTrees]   = Chest("Farm", 1, 0),
            [TaskKind.ClearRocks] = Chest("Farm", 2, 0),
            [TaskKind.ClearWeeds] = Chest("Shed1", 5, 5),
        };
        var forward = new List<BufferedItem>
        {
            Item("(O)1", TaskKind.CutTrees),
            Item("(O)2", TaskKind.ClearRocks),
            Item("(O)3", TaskKind.ClearWeeds),
        };
        var reversed = Enumerable.Reverse(forward).ToList();

        var a = new DepositPlanner().Plan(forward, assignments, Bin, Start, Metric);
        var b = new DepositPlanner().Plan(reversed, assignments, Bin, Start, Metric);

        Assert.Equal(
            a.Trips.Select(t => t.Tile),
            b.Trips.Select(t => t.Tile));
    }
}
