using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Inventory;

public sealed class DepositPlannerTests
{
    private static readonly TileCoord BinTile = new(71, 13);
    private static readonly TileCoord Start   = new(0, 0);

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static DestinationKey Resolve(
        TaskKind task, IReadOnlyDictionary<TaskKind, DestinationKey> assignments) =>
        assignments.TryGetValue(task, out var d) && d is not null ? d : MailDestination.Instance;

    private static Dictionary<string, int> Totals(IEnumerable<(string id, int qty)> items)
    {
        var map = new Dictionary<string, int>();
        foreach (var (id, qty) in items)
            map[id] = map.TryGetValue(id, out var e) ? e + qty : qty;
        return map;
    }

    private static BufferedItem Buffered(string itemId, int quantity, TaskKind task, OutputScopeProvenance? provenance = null) =>
        new(itemId, quantity, task, provenance ?? OutputScopeProvenance.Unknown);

    // PBT-U14-01: conservation — every buffered item appears exactly once across trips ∪ pre-mail.
    [Property(MaxTest = 1000, Replay = "")]
    public Property Conservation_Holds()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            var inTotals = Totals(snapshot.Select(b => (b.QualifiedItemId, b.Quantity)));
            var outTotals = Totals(
                plan.Trips.SelectMany(t => t.Items).Concat(plan.PreMailedOverflow)
                    .Select(s => (s.QualifiedItemId, s.Quantity)));

            bool equal = inTotals.Count == outTotals.Count
                         && inTotals.All(kv => outTotals.TryGetValue(kv.Key, out var v) && v == kv.Value);

            return equal.Label($"in={inTotals.Count} ids/{inTotals.Values.Sum()} qty; " +
                               $"out={outTotals.Count} ids/{outTotals.Values.Sum()} qty");
        });
    }

    // PBT-U14-02: trip count equals the number of distinct walkable (chest/bin) destinations present.
    [Property(MaxTest = 1000, Replay = "")]
    public Property TripCount_Equals_Distinct_Walkable_Destinations()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            var expected = snapshot
                .Select(b => Resolve(b.SourceTask, assignments))
                .Where(d => d is ChestDestination or ShippingBinDestination)
                .Distinct()
                .Count();

            return (plan.Trips.Count == expected)
                .Label($"trips={plan.Trips.Count} expected={expected}");
        });
    }

    // PBT-U14-03: no trip is empty and no trip targets MailDestination.
    [Property(MaxTest = 1000, Replay = "")]
    public Property No_Empty_Or_Mail_Trips()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            return plan.Trips.All(t => t.Items.Count > 0 && t.Destination is not MailDestination)
                .Label($"trips={plan.Trips.Count}");
        });
    }

    // PBT-U14-04: resolution totality — total quantity in equals total quantity out (nothing lost/created).
    [Property(MaxTest = 1000, Replay = "")]
    public Property Total_Quantity_Preserved()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            int inQty  = snapshot.Sum(b => b.Quantity);
            int outQty = plan.Trips.SelectMany(t => t.Items).Sum(s => s.Quantity)
                         + plan.PreMailedOverflow.Sum(s => s.Quantity);

            return (inQty == outQty).Label($"in={inQty} out={outQty}");
        });
    }

    // Sanity (not a hard property): nearest-neighbor visits the closer chest first.
    [Fact]
    public void Orders_Trips_Nearest_First()
    {
        var near = new ChestRef("Farm", new TileCoord(2, 0));
        var far  = new ChestRef("Farm", new TileCoord(50, 0));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)388", 1, TaskKind.CutTrees),
            Buffered("(O)390", 1, TaskKind.ClearRocks),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CutTrees]   = new ChestDestination(far),
            [TaskKind.ClearRocks] = new ChestDestination(near),
        };

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, new TileCoord(0, 0), Manhattan);

        Assert.Equal(2, plan.Trips.Count);
        Assert.Equal(near.Tile, plan.Trips[0].Tile);
        Assert.Equal(far.Tile, plan.Trips[1].Tile);
    }

    // Unassigned output resolves to mail, not a trip (FD-Q2=A / FR-OUT-04).
    [Fact]
    public void Unassigned_Task_Output_Goes_To_PreMail()
    {
        var snapshot = new List<BufferedItem> { Buffered("(O)709", 5, TaskKind.CutTrees) };
        var assignments = new Dictionary<TaskKind, DestinationKey>(); // empty → CutTrees unassigned

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        Assert.Empty(plan.Trips);
        Assert.Single(plan.PreMailedOverflow);
        Assert.Equal(5, plan.PreMailedOverflow[0].Quantity);
    }

    [Fact]
    public void Provenance_Does_Not_Change_TaskOwned_Destination_Resolution()
    {
        var chest = new ChestRef("Farm", new TileCoord(4, 4));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)388", 1, TaskKind.CutTrees, OutputScopeProvenance.Outdoor()),
            Buffered("(O)388", 2, TaskKind.CutTrees, OutputScopeProvenance.Greenhouse("Greenhouse")),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CutTrees] = new ChestDestination(chest),
        };

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(chest.Tile, trip.Tile);
        Assert.All(trip.Items, item => Assert.Equal(TaskKind.CutTrees, item.SourceTask));
        Assert.Equal(3, trip.Items.Sum(item => item.Quantity));
    }
}
