using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

public class ShiftPlanBuilderTests
{
    private static readonly HashSet<string> AnimalHouses = new(StringComparer.Ordinal)
    {
        "Barn",
        "Big Barn",
        "Deluxe Barn",
        "Coop",
        "Big Coop",
        "Deluxe Coop",
    };

    private readonly ShiftPlanBuilder _sut = new();

    [Fact]
    public void BarnOnly_CreatesAnimalBuildingBatchWithFeedFlag()
    {
        var zones = new[] { Zone("Barn") };

        var result = _sut.BuildBatchPlan(zones, Enabled(TaskKind.FeedAnimals), IsAnimalHouse);

        var batch = Assert.Single(result);
        Assert.Equal("Barn", batch.LocationName);
        Assert.Equal(BatchKind.AnimalBuilding, batch.Kind);
        Assert.True(batch.FeedBuilding);
        Assert.Empty(batch.TileWork);
        Assert.Empty(batch.AnimalWork);
    }

    [Fact]
    public void GreenhouseOnly_CreatesInteriorBatchWithoutFeedFlag()
    {
        var zones = new[] { Zone("Greenhouse") };

        var result = _sut.BuildBatchPlan(zones, Enabled(TaskKind.FeedAnimals), IsAnimalHouse);

        var batch = Assert.Single(result);
        Assert.Equal("Greenhouse", batch.LocationName);
        Assert.Equal(BatchKind.Interior, batch.Kind);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void MixedZones_AreOrderedAnimalInteriorOutdoor()
    {
        var zones = new[]
        {
            Zone("Farm"),
            Zone("Greenhouse"),
            Zone("Barn"),
            Zone("Coop"),
        };

        var result = _sut.BuildBatchPlan(zones, Enabled(TaskKind.FeedAnimals), IsAnimalHouse);

        Assert.Equal(
            new[] { BatchKind.AnimalBuilding, BatchKind.AnimalBuilding, BatchKind.Interior, BatchKind.OutdoorFarm },
            result.Select(batch => batch.Kind));
        Assert.Equal(new[] { "Barn", "Coop", "Greenhouse", "Farm" }, result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void OutdoorOnly_CreatesSingleOutdoorFarmBatch()
    {
        var zones = new[] { Zone("Farm"), Zone("Farm", 3, 3) };

        var result = _sut.BuildBatchPlan(zones, Enabled(TaskKind.ClearWeeds), IsAnimalHouse);

        var batch = Assert.Single(result);
        Assert.Equal("Farm", batch.LocationName);
        Assert.Equal(BatchKind.OutdoorFarm, batch.Kind);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void EmptyInput_CreatesNoBatches()
    {
        var result = _sut.BuildBatchPlan(Array.Empty<Zone>(), Enabled(TaskKind.FeedAnimals), IsAnimalHouse);

        Assert.Empty(result);
    }

    [Fact]
    public void AnimalPriorityOrder_RemainsFeedPetCollect()
    {
        var orderer = new TaskPriorityOrderer();

        var ordered = orderer.Order(new[]
        {
            TaskKind.CollectAnimalProducts,
            TaskKind.PetAnimals,
            TaskKind.FeedAnimals,
        });

        Assert.Equal(
            new[] { TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.CollectAnimalProducts },
            ordered);
    }

    [Property(MaxTest = 1000)]
    public Property AnyZoneSet_MapsEachLocationToOneOrderedBatch()
    {
        return Prop.ForAll(ZoneList(), zones =>
        {
            var result = _sut.BuildBatchPlan(zones, Enabled(TaskKind.FeedAnimals), IsAnimalHouse);
            var normalizedLocations = zones
                .Select(zone => string.IsNullOrWhiteSpace(zone.LocationName) ? "Farm" : zone.LocationName)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var eachLocationMappedOnce = normalizedLocations.All(location =>
                result.Count(batch => batch.LocationName == location) == 1);

            var noExtraLocations = result.All(batch => normalizedLocations.Contains(batch.LocationName, StringComparer.Ordinal));

            var ordered = result
                .Zip(result.Skip(1), (left, right) => left.Kind <= right.Kind)
                .All(isOrdered => isOrdered);

            var outdoorBatchBounded = result.Count(batch => batch.Kind == BatchKind.OutdoorFarm) <= 1;

            var skeletonsAreEmpty = result.All(batch => batch.TileWork.Count == 0 && batch.AnimalWork.Count == 0);

            return eachLocationMappedOnce &&
                   noExtraLocations &&
                   ordered &&
                   outdoorBatchBounded &&
                   skeletonsAreEmpty;
        });
    }

    private static bool IsAnimalHouse(string locationName) => AnimalHouses.Contains(locationName);

    private static IReadOnlySet<TaskKind> Enabled(params TaskKind[] tasks) => tasks.ToHashSet();

    private static Zone Zone(string locationName, int x = 0, int y = 0) =>
        new(locationName, new TileCoord(x, y), new TileCoord(x + 1, y + 1));

    private static Arbitrary<IReadOnlyList<Zone>> ZoneList()
    {
        var locations = new[] { "Farm", "Greenhouse", "Barn", "Coop", "Shed" };
        var gen =
            from count in Gen.Choose(0, 12)
            from zones in Gen.ListOf(count,
                from location in Gen.Elements(locations)
                from x in Gen.Choose(0, 20)
                from y in Gen.Choose(0, 20)
                select Zone(location, x, y))
            select (IReadOnlyList<Zone>)zones.ToList();

        return Arb.From(gen);
    }
}
