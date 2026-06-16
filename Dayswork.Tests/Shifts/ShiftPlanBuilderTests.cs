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

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority);

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

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.HarvestCrops), TaskKindSets.DefaultCategoryPriority);

        var batch = Assert.Single(result);
        Assert.Equal("Greenhouse", batch.LocationName);
        Assert.Equal(BatchKind.Greenhouse, batch.Kind);
        Assert.Equal(new[] { TaskKind.HarvestCrops }, batch.Tasks);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void MixedScopes_GroupEachBuildingsIndoorAndGrazingThenForageThenCrops()
    {
        // Per-building grouping — each building's interior batch is immediately followed by
        // that building's grazing pass. No CollectAnimalProducts here, so no FarmForage batch.
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
            Enabled(TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.HarvestCrops, TaskKind.ClearWeeds),
            TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[]
            {
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.Greenhouse,
                BatchKind.OutdoorCrops,
                BatchKind.OutdoorClearing,
            },
            result.Select(batch => batch.Kind));
        Assert.Equal(
            new[] { "Barn", "Barn", "Coop", "Coop", "Greenhouse", "Farm", "Farm" },
            result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void TwoBuildings_FeedAndPet_NoCollect_GroupsPerBuilding_NoFarmForage()
    {
        // EX-T09-1
        var scopes = Scopes(animalBuildings: new[]
        {
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
        });

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals, TaskKind.PetAnimals), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[]
            {
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
            },
            result.Select(batch => batch.Kind));
        Assert.Equal(new[] { "Barn", "Barn", "Coop", "Coop" }, result.Select(batch => batch.LocationName));
        Assert.DoesNotContain(result, batch => batch.Kind == BatchKind.FarmForage);
    }

    [Fact]
    public void TwoBuildings_WithCollect_AppendsSingleFarmForageAfterBuildingPairs()
    {
        // EX-T09-2
        var scopes = Scopes(animalBuildings: new[]
        {
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
        });

        var result = _sut.BuildBatchPlan(
            scopes,
            Enabled(TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.CollectAnimalProducts),
            TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[]
            {
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorAnimals,
                BatchKind.FarmForage,
            },
            result.Select(batch => batch.Kind));
        var forage = result.Single(batch => batch.Kind == BatchKind.FarmForage);
        Assert.Equal("Farm", forage.LocationName);
        Assert.Equal(new[] { TaskKind.CollectAnimalProducts }, forage.Tasks);
    }

    [Fact]
    public void SingleBuilding_CollectOnly_ProducesBuildingPairThenFarmForage()
    {
        // EX-T09-3
        var scopes = Scopes(animalBuildings: new[] { new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop) });

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.CollectAnimalProducts), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[] { BatchKind.AnimalBuilding, BatchKind.OutdoorAnimals, BatchKind.FarmForage },
            result.Select(batch => batch.Kind));
        Assert.Equal(new[] { "Coop", "Coop", "Farm" }, result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void FeedOnly_ProducesInteriorBatchesOnly_NoGrazingNoForage()
    {
        // EX-T09-4
        var scopes = Scopes(animalBuildings: new[]
        {
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
        });

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority);

        Assert.All(result, batch => Assert.Equal(BatchKind.AnimalBuilding, batch.Kind));
        Assert.Equal(new[] { "Barn", "Coop" }, result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void OutdoorClearingOnly_CreatesSingleOutdoorClearingBatch()
    {
        var scopes = Scopes(outdoor: new OutdoorWorkScope(new[] { Zone("Farm"), Zone("Farm", 3, 3) }, 2));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.ClearWeeds), TaskKindSets.DefaultCategoryPriority);

        var batch = Assert.Single(result);
        Assert.Equal("Farm", batch.LocationName);
        Assert.Equal(BatchKind.OutdoorClearing, batch.Kind);
        Assert.Equal(new[] { TaskKind.ClearWeeds }, batch.Tasks);
        Assert.False(batch.FeedBuilding);
    }

    [Fact]
    public void EmptyScopes_CreatesNoBatches()
    {
        var result = _sut.BuildBatchPlan(Scopes(), Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority);

        Assert.Empty(result);
    }

    [Fact]
    public void CropsPriorityFirst_PutsCropAndClearingBatchesBeforeAnimalBatches()
    {
        var cropFirstPriority = new[]
        {
            TaskCategory.Crops,
            TaskCategory.AnimalCare,
            TaskCategory.Fieldwork,
        };
        var scopes = Scopes(
            outdoor: new OutdoorWorkScope(new[] { Zone("Farm") }, 1),
            animalBuildings: new[] { new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop) },
            greenhouse: new GreenhouseWorkScope("Greenhouse"));

        var result = _sut.BuildBatchPlan(
            scopes,
            Enabled(TaskKind.HarvestCrops, TaskKind.FeedAnimals, TaskKind.ClearWeeds),
            cropFirstPriority);

        Assert.Equal(
            new[]
            {
                BatchKind.Greenhouse,
                BatchKind.OutdoorCrops,
                BatchKind.AnimalBuilding,
                BatchKind.OutdoorClearing,
            },
            result.Select(batch => batch.Kind));
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
    public Property AnyScopeShape_ProducesPerBuildingGroupedPlan()
    {
        return Prop.ForAll(ScopeSetGen(), EnabledTasksGen(), (scopes, enabledTasks) =>
        {
            var result = _sut.BuildBatchPlan(scopes, enabledTasks, TaskKindSets.DefaultCategoryPriority);

            var sortedBuildings = scopes.AnimalBuildings
                .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                .ThenBy(building => building.Tier)
                .Select(building => building.LocationName)
                .ToList();

            var anyAnimalTask = enabledTasks.Any(TaskKindSets.IsAnimalService);
            var nonFeedAnimal = enabledTasks.Any(task => TaskKindSets.IsAnimalService(task) && task != TaskKind.FeedAnimals);
            var collectEnabled = enabledTasks.Contains(TaskKind.CollectAnimalProducts);

            // P-T09-2: AnimalBuilding names == sorted buildings (when any animal task enabled), else none.
            var animalBuildingNames = result
                .Where(batch => batch.Kind == BatchKind.AnimalBuilding)
                .Select(batch => batch.LocationName)
                .ToList();
            var buildingsOk = animalBuildingNames.SequenceEqual(anyAnimalTask ? sortedBuildings : new List<string>());

            // P-T09-4: one OutdoorAnimals (grazing) batch per building when a non-feed animal task is enabled, else none.
            var grazingOk = result.Count(batch => batch.Kind == BatchKind.OutdoorAnimals)
                            == (nonFeedAnimal ? sortedBuildings.Count : 0);

            // P-T09-1: each AnimalBuilding is immediately followed by its own OutdoorAnimals (same
            // LocationName) when non-feed animal tasks are enabled; each OutdoorAnimals is immediately
            // preceded by its AnimalBuilding.
            var pairingOk = true;
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i].Kind == BatchKind.AnimalBuilding && nonFeedAnimal)
                    pairingOk &= i + 1 < result.Count &&
                                 result[i + 1].Kind == BatchKind.OutdoorAnimals &&
                                 result[i + 1].LocationName == result[i].LocationName;

                if (result[i].Kind == BatchKind.OutdoorAnimals)
                    pairingOk &= i > 0 &&
                                 result[i - 1].Kind == BatchKind.AnimalBuilding &&
                                 result[i - 1].LocationName == result[i].LocationName;
            }

            // P-T09-3: at most one FarmForage, present iff Collect enabled, positioned after all
            // AnimalBuilding/OutdoorAnimals batches and before any Greenhouse/OutdoorCrops/OutdoorClearing.
            var forageCount = result.Count(batch => batch.Kind == BatchKind.FarmForage);
            var forageOk = forageCount <= 1 && (forageCount == 1) == collectEnabled;
            if (forageCount == 1)
            {
                var fi = MinIndex(result, batch => batch.Kind == BatchKind.FarmForage);
                var animalIdxMax = MaxIndex(result, batch => batch.Kind is BatchKind.AnimalBuilding or BatchKind.OutdoorAnimals);
                var cropIdxMin = MinIndex(result, batch => batch.Kind is BatchKind.Greenhouse or BatchKind.OutdoorCrops or BatchKind.OutdoorClearing);
                forageOk &= (animalIdxMax < 0 || animalIdxMax < fi) &&
                            (cropIdxMin < 0 || fi < cropIdxMin);
            }

            // P-T09-5: bounded non-animal families.
            var boundedFamilies =
                result.Count(batch => batch.Kind == BatchKind.Greenhouse) <= 1 &&
                result.Count(batch => batch.Kind == BatchKind.OutdoorCrops) <= 1 &&
                result.Count(batch => batch.Kind == BatchKind.OutdoorClearing) <= 1;

            // P-T09-6: skeletons carry no filled work.
            var skeletonsAreEmpty = result.All(batch => batch.TileWork.Count == 0 && batch.AnimalWork.Count == 0);

            return buildingsOk && grazingOk && pairingOk && forageOk && boundedFamilies && skeletonsAreEmpty;
        });
    }

    private static int MaxIndex(IReadOnlyList<WorkBatch> batches, Func<WorkBatch, bool> predicate)
    {
        var index = -1;
        for (var i = 0; i < batches.Count; i++)
            if (predicate(batches[i]))
                index = i;
        return index;
    }

    private static int MinIndex(IReadOnlyList<WorkBatch> batches, Func<WorkBatch, bool> predicate)
    {
        for (var i = 0; i < batches.Count; i++)
            if (predicate(batches[i]))
                return i;
        return -1;
    }

    private static WorkScopeSet Scopes(
        OutdoorWorkScope? outdoor = null,
        IReadOnlyList<AnimalBuildingScope>? animalBuildings = null,
        GreenhouseWorkScope? greenhouse = null) =>
        WorkScopeSet.WithSingleGreenhouse(
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
