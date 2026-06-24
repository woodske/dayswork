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
        assignments.TryGetValue(task, out var d) && d is not null ? d : AutomaticOutputDestination.Instance;

    private static Dictionary<(string Id, int Quality), int> Totals(IEnumerable<(string id, int quality, int qty)> items)
    {
        var map = new Dictionary<(string, int), int>();
        foreach (var (id, quality, qty) in items)
        {
            var key = (id, quality);
            map[key] = map.TryGetValue(key, out var e) ? e + qty : qty;
        }
        return map;
    }

    private static BufferedItem Buffered(string itemId, int quantity, TaskKind task, OutputScopeProvenance? provenance = null, int quality = 0) =>
        new(itemId, quantity, task, provenance ?? OutputScopeProvenance.Unknown, quality);

    // Conservation — every buffered item appears exactly once across trips ∪ automatic overflow,
    // keyed by (itemId, quality) so different-quality stacks are tracked separately.
    [Property(MaxTest = 1000, Replay = "")]
    public Property Conservation_Holds()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            var inTotals = Totals(snapshot.Select(b => (b.QualifiedItemId, b.Quality, b.Quantity)));
            var outTotals = Totals(
                plan.Trips.SelectMany(t => t.Items).Concat(plan.AutomaticOverflow)
                    .Select(s => (s.QualifiedItemId, s.Quality, s.Quantity)));

            bool equal = inTotals.Count == outTotals.Count
                         && inTotals.All(kv => outTotals.TryGetValue(kv.Key, out var v) && v == kv.Value);

            return equal.Label($"in={inTotals.Count} ids/{inTotals.Values.Sum()} qty; " +
                               $"out={outTotals.Count} ids/{outTotals.Values.Sum()} qty");
        });
    }

    [Property(MaxTest = 1000, Replay = "")]
    public Property Conservation_Holds_With_ProvenanceAssignments()
    {
        return Prop.ForAll(DepositInputGen.PlannerInputWithProvenance(), input =>
        {
            var (snapshot, taskAssignments, provenanceAssignments) = input;
            var plan = new DepositPlanner().Plan(
                snapshot,
                taskAssignments,
                provenanceAssignments,
                BinTile,
                Start,
                Manhattan);

            var inTotals = Totals(snapshot.Select(b => (b.QualifiedItemId, b.Quality, b.Quantity)));
            var outTotals = Totals(
                plan.Trips.SelectMany(t => t.Items).Concat(plan.AutomaticOverflow)
                    .Select(s => (s.QualifiedItemId, s.Quality, s.Quantity)));

            return (inTotals.Count == outTotals.Count
                    && inTotals.All(kv => outTotals.TryGetValue(kv.Key, out var v) && v == kv.Value))
                .Label($"in={inTotals.Values.Sum()} out={outTotals.Values.Sum()}");
        });
    }

    // Trip count equals the number of distinct walkable (chest/bin) destinations present.
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

    // No trip is empty and no trip targets AutomaticOutputDestination.
    [Property(MaxTest = 1000, Replay = "")]
    public Property No_Empty_Or_AutomaticOutput_Trips()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            return plan.Trips.All(t => t.Items.Count > 0 && t.Destination is not AutomaticOutputDestination)
                .Label($"trips={plan.Trips.Count}");
        });
    }

    // Resolution totality — total quantity in equals total quantity out (nothing lost/created).
    [Property(MaxTest = 1000, Replay = "")]
    public Property Total_Quantity_Preserved()
    {
        return Prop.ForAll(DepositInputGen.PlannerInput(), input =>
        {
            var (snapshot, assignments) = input;
            var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

            int inQty  = snapshot.Sum(b => b.Quantity);
            int outQty = plan.Trips.SelectMany(t => t.Items).Sum(s => s.Quantity)
                         + plan.AutomaticOverflow.Sum(s => s.Quantity);

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

    // Unassigned output resolves to automatic overflow, not a trip.
    [Fact]
    public void Unassigned_Task_Output_Goes_To_AutomaticOverflow()
    {
        var snapshot = new List<BufferedItem> { Buffered("(O)709", 5, TaskKind.CutTrees) };
        var assignments = new Dictionary<TaskKind, DestinationKey>(); // empty → CutTrees unassigned

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        Assert.Empty(plan.Trips);
        Assert.Single(plan.AutomaticOverflow);
        Assert.Equal(5, plan.AutomaticOverflow[0].Quantity);
    }

    [Fact]
    public void CollectFruit_Assigned_To_ShippingBin_Creates_Bin_Trip_Not_AutomaticOverflow()
    {
        var snapshot = new List<BufferedItem> { Buffered("(O)634", 3, TaskKind.CollectFruit) };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.CollectFruit] = ShippingBinDestination.Instance,
        };
        var binTile = new TileCoord(12, 34);

        var plan = new DepositPlanner().Plan(snapshot, assignments, binTile, Start, Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.IsType<ShippingBinDestination>(trip.Destination);
        Assert.Equal(binTile, trip.Tile);
        Assert.Empty(plan.AutomaticOverflow);
        var item = Assert.Single(trip.Items);
        Assert.Equal("(O)634", item.QualifiedItemId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(TaskKind.CollectFruit, item.SourceTask);
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

    [Fact]
    public void SameFlavorId_Consolidates_And_Carries_FlavorId()
    {
        var chest = new ChestRef("Farm", new TileCoord(4, 4));
        // Two Sturgeon Roe stacks (same base id (O)812, same flavor token) from two ponds.
        var snapshot = new List<BufferedItem>
        {
            new("(O)812", 2, TaskKind.HarvestCrops, OutputScopeProvenance.FishPond(), 0, "(O)812|sturgeon|Roe|0"),
            new("(O)812", 3, TaskKind.HarvestCrops, OutputScopeProvenance.FishPond(), 0, "(O)812|sturgeon|Roe|0"),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey> { [TaskKind.HarvestCrops] = new ChestDestination(chest) };

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        var trip = Assert.Single(plan.Trips);
        var item = Assert.Single(trip.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal("(O)812|sturgeon|Roe|0", item.FlavorId);
    }

    [Fact]
    public void DifferentFlavorIds_SameBaseId_StayDistinct()
    {
        var chest = new ChestRef("Farm", new TileCoord(4, 4));
        // Sturgeon Roe vs Sardine Roe — same base id (O)812, different flavor tokens.
        var snapshot = new List<BufferedItem>
        {
            new("(O)812", 2, TaskKind.HarvestCrops, OutputScopeProvenance.FishPond(), 0, "(O)812|sturgeon|Roe|0"),
            new("(O)812", 3, TaskKind.HarvestCrops, OutputScopeProvenance.FishPond(), 0, "(O)812|sardine|Roe|0"),
        };
        var assignments = new Dictionary<TaskKind, DestinationKey> { [TaskKind.HarvestCrops] = new ChestDestination(chest) };

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(2, trip.Items.Count);
        Assert.Contains(trip.Items, i => i.FlavorId == "(O)812|sturgeon|Roe|0" && i.Quantity == 2);
        Assert.Contains(trip.Items, i => i.FlavorId == "(O)812|sardine|Roe|0" && i.Quantity == 3);
    }

    [Fact]
    public void FlavorId_Survives_To_AutomaticOverflow()
    {
        var snapshot = new List<BufferedItem>
        {
            new("(O)812", 4, TaskKind.HarvestCrops, OutputScopeProvenance.FishPond(), 0, "(O)812|sturgeon|Roe|0"),
        };

        var plan = new DepositPlanner().Plan(snapshot, new Dictionary<TaskKind, DestinationKey>(), BinTile, Start, Manhattan);

        Assert.Empty(plan.Trips);
        var overflow = Assert.Single(plan.AutomaticOverflow);
        Assert.Equal("(O)812|sturgeon|Roe|0", overflow.FlavorId);
        Assert.Equal(4, overflow.Quantity);
    }

    [Fact]
    public void ProvenanceAssignment_Wins_Before_TaskAssignment()
    {
        var managed = OutputScopeProvenance.ManagedCrop("group-a|Farm|0|0|1|1");
        var managedChest = new ChestRef("Farm", new TileCoord(8, 8));
        var taskChest = new ChestRef("Farm", new TileCoord(2, 2));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)24", 3, TaskKind.HarvestCrops, managed),
        };
        var taskAssignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.HarvestCrops] = new ChestDestination(taskChest),
        };
        var provenanceAssignments = new Dictionary<OutputScopeProvenance, DestinationKey>
        {
            [managed] = new ChestDestination(managedChest),
        };

        var plan = new DepositPlanner().Plan(
            snapshot,
            taskAssignments,
            provenanceAssignments,
            BinTile,
            Start,
            Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(managedChest.Tile, trip.Tile);
    }

    [Fact]
    public void MissingProvenanceAssignment_FallsBack_To_TaskAssignment()
    {
        var taskChest = new ChestRef("Farm", new TileCoord(2, 2));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)24", 3, TaskKind.HarvestCrops, OutputScopeProvenance.ManagedCrop("missing")),
        };
        var taskAssignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.HarvestCrops] = new ChestDestination(taskChest),
        };

        var plan = new DepositPlanner().Plan(
            snapshot,
            taskAssignments,
            new Dictionary<OutputScopeProvenance, DestinationKey>(),
            BinTile,
            Start,
            Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(taskChest.Tile, trip.Tile);
    }

    [Fact]
    public void ManagedProvenanceMap_Does_Not_Reroute_OrdinaryHarvest()
    {
        var managed = OutputScopeProvenance.ManagedCrop("group-a|Farm|0|0|1|1");
        var managedChest = new ChestRef("Farm", new TileCoord(8, 8));
        var ordinaryChest = new ChestRef("Farm", new TileCoord(2, 2));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)24", 3, TaskKind.HarvestCrops, OutputScopeProvenance.Outdoor()),
        };
        var taskAssignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.HarvestCrops] = new ChestDestination(ordinaryChest),
        };
        var provenanceAssignments = new Dictionary<OutputScopeProvenance, DestinationKey>
        {
            [managed] = new ChestDestination(managedChest),
        };

        var plan = new DepositPlanner().Plan(
            snapshot,
            taskAssignments,
            provenanceAssignments,
            BinTile,
            Start,
            Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(ordinaryChest.Tile, trip.Tile);
    }

    [Fact]
    public void AutomaticOutput_Preserves_Provenance_For_Overflow()
    {
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)430", 1, TaskKind.CollectAnimalProducts, OutputScopeProvenance.AnimalBuilding("Barn")),
        };

        var plan = new DepositPlanner().Plan(snapshot, new Dictionary<TaskKind, DestinationKey>(), BinTile, Start, Manhattan);

        var overflow = Assert.Single(plan.AutomaticOverflow);
        Assert.Equal(OutputScopeFamily.AnimalBuilding, overflow.Provenance.Family);
        Assert.Equal("Barn", overflow.Provenance.ScopeName);
    }

    [Fact]
    public void UndeliveredFallbackPolicy_Ships_Bin_Trips_But_Overflows_Chest_Trips()
    {
        var chest = new ChestDestination(new ChestRef("Farm", new TileCoord(4, 4)));

        Assert.Equal(
            UndeliveredDepositResolution.ShippingBin,
            DepositPlanner.ResolveUndelivered(ShippingBinDestination.Instance));
        Assert.Equal(
            UndeliveredDepositResolution.AutomaticOverflow,
            DepositPlanner.ResolveUndelivered(chest));
    }

    // Regression: items of the same kind but different quality must not be merged.
    // Before the fix, BufferedItem had no Quality field and the planner's grouping key omitted
    // quality — a gold parsnip and a regular parsnip targeting the same chest were summed into
    // one stack and both came out regular quality at deposit time.
    [Fact]
    public void DifferentQuality_Items_Are_Not_Merged_Into_Single_Stack()
    {
        var chest = new ChestRef("Farm", new TileCoord(4, 4));
        var snapshot = new List<BufferedItem>
        {
            Buffered("(O)24", 5, TaskKind.HarvestCrops, quality: 0),  // regular
            Buffered("(O)24", 3, TaskKind.HarvestCrops, quality: 2),  // gold
        };
        var assignments = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.HarvestCrops] = new ChestDestination(chest),
        };

        var plan = new DepositPlanner().Plan(snapshot, assignments, BinTile, Start, Manhattan);

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(2, trip.Items.Count);
        Assert.Contains(trip.Items, s => s.Quality == 0 && s.Quantity == 5);
        Assert.Contains(trip.Items, s => s.Quality == 2 && s.Quantity == 3);
    }
}
