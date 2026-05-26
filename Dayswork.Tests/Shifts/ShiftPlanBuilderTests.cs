using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ShiftPlanBuilderTests
{
    private static readonly AnimalBuildingTier[] AnimalTiers = Enum.GetValues<AnimalBuildingTier>();
    private readonly ShiftPlanBuilder _sut = new();

    [Fact]
    public void AnimalBuildingOnly_CreatesAnimalBuildingBatchWithFeedFlag()
    {
        var scopes = Scopes(animalBuildings: new[] { new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn) });

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals));

        var batch = Assert.Single(result);
        Assert.Equal("Barn", batch.LocationName);
        Assert.Equal(BatchKind.AnimalBuilding, batch.Kind);
        Assert.Equal(new[] { TaskKind.FeedAnimals }, batch.Tasks);
        Assert.True(batch.FeedBuilding);
        Assert.Empty(batch.TileWork);
        Assert.Empty(batch.AnimalWork);
    }

    [Fact]
    public void GreenhouseOnly_CreatesDedicatedGreenhouseBatch()
    {
        var scopes = Scopes(greenhouse: new GreenhouseWorkScope("Greenhouse"));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.HarvestCrops));

        var batch = Assert.Single(result);
        Assert.Equal("Greenhouse", batch.LocationName);
        Assert.Equal(BatchKind.Greenhouse, batch.Kind);
        Assert.Equal(new[] { TaskKind.HarvestCrops }, batch.Tasks);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void MixedScopes_AreOrderedAnimalOutdoorAnimalsGreenhouseOutdoorCropsOutdoorClearing()
    {
        var scopes = Scopes(
            outdoor: new OutdoorWorkScope(new[] { Zone("Farm") }, 1),
            animalBuildings: new[]
            {
                new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
                new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
            },
            greenhouse: new GreenhouseWorkScope("Greenhouse"));

        var result = _sut.BuildBatchPlan(
            scopes,
            Enabled(TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.HarvestCrops, TaskKind.ClearWeeds));

        Assert.Equal(
            new[]
            {
                BatchKind.AnimalBuilding,
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.Greenhouse,
                BatchKind.OutdoorCrops,
                BatchKind.OutdoorClearing,
            },
            result.Select(batch => batch.Kind));
        Assert.Equal(
            new[] { "Barn", "Coop", "Farm", "Greenhouse", "Farm", "Farm" },
            result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void OutdoorClearingOnly_CreatesSingleOutdoorClearingBatch()
    {
        var scopes = Scopes(outdoor: new OutdoorWorkScope(new[] { Zone("Farm"), Zone("Farm", 3, 3) }, 2));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.ClearWeeds));

        var batch = Assert.Single(result);
        Assert.Equal("Farm", batch.LocationName);
        Assert.Equal(BatchKind.OutdoorClearing, batch.Kind);
        Assert.Equal(new[] { TaskKind.ClearWeeds }, batch.Tasks);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void EmptyScopes_CreatesNoBatches()
    {
        var result = _sut.BuildBatchPlan(Scopes(), Enabled(TaskKind.FeedAnimals));

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

    [Property(MaxTest = 500)]
    public Property AnyScopeShape_ProducesBoundedOrderedSkeletons()
    {
        return Prop.ForAll(ScopeSetGen(), EnabledTasksGen(), (scopes, enabledTasks) =>
        {
            var result = _sut.BuildBatchPlan(scopes, enabledTasks);

            var ordered = result
                .Zip(result.Skip(1), (left, right) => left.Kind <= right.Kind)
                .All(isOrdered => isOrdered);

            var animalBuildingCount = result.Count(batch => batch.Kind == BatchKind.AnimalBuilding);
            var expectedAnimalBuildingCount =
                enabledTasks.Any(TaskKindSets.IsAnimalService)
                    ? scopes.AnimalBuildings.Count
                    : 0;

            var boundedFamilies =
                result.Count(batch => batch.Kind == BatchKind.OutdoorAnimals) <= 1 &&
                result.Count(batch => batch.Kind == BatchKind.Greenhouse) <= 1 &&
                result.Count(batch => batch.Kind == BatchKind.OutdoorCrops) <= 1 &&
                result.Count(batch => batch.Kind == BatchKind.OutdoorClearing) <= 1;

            var skeletonsAreEmpty = result.All(batch => batch.TileWork.Count == 0 && batch.AnimalWork.Count == 0);

            return ordered &&
                   boundedFamilies &&
                   skeletonsAreEmpty &&
                   animalBuildingCount == expectedAnimalBuildingCount;
        });
    }

    private static WorkScopeSet Scopes(
        OutdoorWorkScope? outdoor = null,
        IReadOnlyList<AnimalBuildingScope>? animalBuildings = null,
        GreenhouseWorkScope? greenhouse = null) =>
        new(
            outdoor,
            animalBuildings ?? Array.Empty<AnimalBuildingScope>(),
            greenhouse);

    private static IReadOnlySet<TaskKind> Enabled(params TaskKind[] tasks) => tasks.ToHashSet();

    private static Zone Zone(string locationName, int x = 0, int y = 0) =>
        new(locationName, new TileCoord(x, y), new TileCoord(x + 1, y + 1));

    private static Arbitrary<WorkScopeSet> ScopeSetGen()
    {
        var gen =
            from outdoorSelected in Arb.Generate<bool>()
            from outdoorTileCount in Gen.Choose(1, 4)
            from animalCount in Gen.Choose(0, 3)
            from animalBuildings in Gen.ListOf(animalCount,
                from tier in Gen.Elements(AnimalTiers)
                from index in Gen.Choose(1, 3)
                select new AnimalBuildingScope($"{tier}-{index}", tier))
            from greenhouseSelected in Arb.Generate<bool>()
            select Scopes(
                outdoorSelected
                    ? new OutdoorWorkScope(
                        Enumerable.Range(0, outdoorTileCount).Select(index => Zone("Farm", index, index)).ToList(),
                        outdoorTileCount)
                    : null,
                animalBuildings
                    .Distinct()
                    .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                    .ThenBy(building => building.Tier)
                    .ToList(),
                greenhouseSelected ? new GreenhouseWorkScope("Greenhouse") : null);

        return Arb.From(gen);
    }

    private static Arbitrary<IReadOnlySet<TaskKind>> EnabledTasksGen()
    {
        var allTasks = Enum.GetValues<TaskKind>();
        var gen =
            from tasks in Gen.SubListOf(allTasks)
            select (IReadOnlySet<TaskKind>)tasks.ToHashSet();

        return Arb.From(gen);
    }
}
