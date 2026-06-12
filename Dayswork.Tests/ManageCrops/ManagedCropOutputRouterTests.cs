namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class ManagedCropOutputRouterTests
{
    private static CropDescriptor Parsnip() =>
        new("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });

    private static CropZoneAssignment Assignment(
        string groupId,
        string locationName,
        TileCoord topLeft,
        TileCoord bottomRight,
        DestinationKey? outputDestination = null)
    {
        return new CropZoneAssignment(
            new Zone(locationName, topLeft, bottomRight),
            string.Equals(locationName, "Farm", StringComparison.Ordinal)
                ? CropAssignmentMode.Seasonal
                : CropAssignmentMode.SeasonAgnostic,
            new[] { new SeasonCropChoice(Season.Spring, Parsnip()) },
            outputDestination,
            groupId);
    }

    [Fact]
    public void AssignmentKey_IsStable_ForSameAssignment()
    {
        var assignment = Assignment("group-a", "Farm", new TileCoord(1, 2), new TileCoord(3, 4));

        Assert.Equal(
            ManagedCropOutputRouter.AssignmentKey(assignment),
            ManagedCropOutputRouter.AssignmentKey(assignment));
    }

    [Fact]
    public void AssignmentKey_Distinguishes_Location_And_ZoneBounds()
    {
        var farm = Assignment("group-a", "Farm", new TileCoord(1, 2), new TileCoord(3, 4));
        var greenhouse = Assignment("group-a", "Greenhouse", new TileCoord(1, 2), new TileCoord(3, 4));
        var shifted = Assignment("group-a", "Farm", new TileCoord(1, 2), new TileCoord(4, 4));

        Assert.NotEqual(ManagedCropOutputRouter.AssignmentKey(farm), ManagedCropOutputRouter.AssignmentKey(greenhouse));
        Assert.NotEqual(ManagedCropOutputRouter.AssignmentKey(farm), ManagedCropOutputRouter.AssignmentKey(shifted));
    }

    [Fact]
    public void BuildDestinationMap_IncludesOnlyAssignmentsWithOutputDestination()
    {
        var chest = new ChestRef("Farm", new TileCoord(8, 8));
        var routed = Assignment("group-a", "Farm", new TileCoord(1, 2), new TileCoord(3, 4), new ChestDestination(chest));
        var automatic = Assignment("group-b", "Farm", new TileCoord(5, 6), new TileCoord(7, 8));

        var map = ManagedCropOutputRouter.BuildDestinationMap(new[] { routed, automatic });

        var entry = Assert.Single(map);
        Assert.Equal(ManagedCropOutputRouter.ProvenanceFor(routed), entry.Key);
        var destination = Assert.IsType<ChestDestination>(entry.Value);
        Assert.Equal(chest, destination.Ref);
    }

    [Fact]
    public void BuildDestinationMap_ShippingBin_CreatesShippingBinDestination()
    {
        var routed = Assignment("group-a", "Farm", new TileCoord(1, 2), new TileCoord(3, 4), ShippingBinDestination.Instance);
        var automatic = Assignment("group-b", "Farm", new TileCoord(5, 6), new TileCoord(7, 8));

        var map = ManagedCropOutputRouter.BuildDestinationMap(new[] { routed, automatic });

        var entry = Assert.Single(map);
        Assert.Equal(ManagedCropOutputRouter.ProvenanceFor(routed), entry.Key);
        Assert.IsType<ShippingBinDestination>(entry.Value);
    }

    [Fact]
    public void CropShiftPlanner_HarvestActionsCarryManagedCropProvenance()
    {
        var planner = new CropShiftPlanner();
        var assignment = Assignment(
            "group-a",
            "Farm",
            new TileCoord(1, 2),
            new TileCoord(1, 2),
            new ChestDestination(new ChestRef("Farm", new TileCoord(8, 8))));
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(1, 2), true, true, false, true, false, true) });

        var result = planner.Plan(
            assignment,
            field,
            new SupplyInventory(new Dictionary<string, int>()),
            Array.Empty<ShopStockSnapshot>(),
            isFestivalDay: false);

        var harvest = Assert.Single(result.SupplyIndependentActions, action => action.Kind == ManagedCropActionKind.Harvest);
        Assert.Equal(ManagedCropOutputRouter.ProvenanceFor(assignment), harvest.OutputProvenance);
    }
}
