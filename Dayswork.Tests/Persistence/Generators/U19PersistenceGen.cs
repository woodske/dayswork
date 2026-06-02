using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Tests.Generators;
using Dayswork.Tests.Pricing;
using FsCheck;

namespace Dayswork.Tests.Persistence.Generators;

public static class U19PersistenceGen
{
    private static readonly TaskKind[] OutputProducingTasks =
    {
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
    };

    public static Arbitrary<Contract> CurrentSchemaContract() =>
        (from scope in ScopeSelection().Generator
         let canonicalScope = CanonicalizeScopeSelection(scope)
         from enabledTasks in EnabledTasksFor(canonicalScope)
         from config in ConfigSnapshotGen.Snapshot().Generator
         from tier in TierGen()
         let preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(canonicalScope, enabledTasks, tier, config)
         where preview.IsValid && preview.ProposedTerms is not null
         from id in Arb.Generate<Guid>().Where(guid => guid != Guid.Empty)
         from schedule in Gen.Elements(ContractSchedule.OneTime, ContractSchedule.Recurring)
         from status in Gen.Elements(ContractStatus.Active, ContractStatus.Paused, ContractStatus.Cancelled)
         from hireDate in GameDateGen()
         from taskDestinations in TaskDestinationsGen(enabledTasks)
         from categoryPriority in CategoryPriorityGen()
         select new Contract(
             Id: new ContractId(id),
             EnabledTasks: enabledTasks,
             TaskDestinations: taskDestinations,
             Schedule: schedule,
             Status: status,
             HireDate: hireDate,
             ScopeSelection: canonicalScope,
             TermsSnapshot: preview.ProposedTerms!,
             Tier: tier,
             CategoryPriority: categoryPriority))
        .ToArbitrary();

    public static Arbitrary<ContractTermsSnapshot> TermsSnapshot() =>
        (from scope in ScopeSelection().Generator
         let canonicalScope = CanonicalizeScopeSelection(scope)
         from enabledTasks in EnabledTasksFor(canonicalScope)
         from config in ConfigSnapshotGen.Snapshot().Generator
         from tier in TierGen()
         let preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(canonicalScope, enabledTasks, tier, config)
         where preview.IsValid && preview.ProposedTerms is not null
         select preview.ProposedTerms)
        .ToArbitrary();

    public static Contract CreateExampleCurrentSchemaContract()
    {
        var scope = new ContractScopeSelection(
            OutdoorZones: new[]
            {
                new Zone("Farm", new TileCoord(0, 0), new TileCoord(5, 5)),
            },
            AnimalBuildings: new[]
            {
                new AnimalBuildingSelection("Barn", AnimalBuildingTier.Barn),
            },
            Greenhouse: new GreenhouseSelection("Greenhouse"));

        var enabledTasks = new HashSet<TaskKind>
        {
            TaskKind.WaterCrops,
            TaskKind.HarvestCrops,
            TaskKind.FeedAnimals,
            TaskKind.CollectAnimalProducts,
            TaskKind.CutTrees,
        };

        var taskDestinations = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.HarvestCrops] = MailDestination.Instance,
            [TaskKind.CollectAnimalProducts] = ShippingBinDestination.Instance,
            [TaskKind.CutTrees] = new ChestDestination(new ChestRef("Farm", new TileCoord(8, 8))),
        };

        return new Contract(
            Id: new ContractId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            EnabledTasks: enabledTasks,
            TaskDestinations: taskDestinations,
            Schedule: ContractSchedule.Recurring,
            Status: ContractStatus.Active,
            HireDate: new GameDate(12, Season.Fall, 2),
            ScopeSelection: scope,
            TermsSnapshot: BuildTerms(scope, enabledTasks, EnergyTier.FullDay),
            Tier: EnergyTier.FullDay,
            CategoryPriority: TaskKindSets.DefaultCategoryPriority);
    }

    public static ContractTermsSnapshot CreateAlternateTermsSnapshot()
    {
        var scope = new ContractScopeSelection(
            OutdoorZones: new[]
            {
                new Zone("Farm", new TileCoord(2, 2), new TileCoord(12, 12)),
            },
            AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
            Greenhouse: null);

        var enabledTasks = new HashSet<TaskKind>
        {
            TaskKind.ClearRocks,
            TaskKind.ClearWeeds,
            TaskKind.ClearGrass,
        };

        return BuildTerms(scope, enabledTasks, EnergyTier.HalfDay);
    }

    public static Arbitrary<IReadOnlyList<Contract>> CurrentSchemaContractList() =>
        Gen.NonEmptyListOf(CurrentSchemaContract().Generator)
            .Select(contracts => contracts
                .GroupBy(contract => contract.Id)
                .Select(group => group.First())
                .OrderBy(contract => contract.Id.Value)
                .Cast<Contract>()
                .ToList()
                .AsReadOnly() as IReadOnlyList<Contract>)
            .ToArbitrary();

    public static Arbitrary<ContractScopeSelection> ScopeSelection() =>
        U18ContractTermsGen.ScopeSelection()
            .Filter(selection =>
                selection.OutdoorZones.Count > 0
                || selection.AnimalBuildings.Count > 0
                || selection.Greenhouse is not null);

    private static Gen<IReadOnlySet<TaskKind>> EnabledTasksFor(ContractScopeSelection scopeSelection)
    {
        var allowed = new List<TaskKind>();
        if (scopeSelection.OutdoorZones.Count > 0)
            allowed.AddRange(TaskKindSets.OutdoorServices);
        if (scopeSelection.AnimalBuildings.Count > 0)
            allowed.AddRange(TaskKindSets.AnimalServices);
        if (scopeSelection.Greenhouse is not null)
            allowed.AddRange(TaskKindSets.GreenhouseServices);

        return Gen.SubListOf(allowed.Distinct().ToArray())
            .Where(tasks => tasks.Count > 0)
            .Select(tasks => (IReadOnlySet<TaskKind>)tasks.ToHashSet());
    }

    private static Gen<IReadOnlyDictionary<TaskKind, DestinationKey>> TaskDestinationsGen(IReadOnlySet<TaskKind> enabledTasks)
    {
        var tasksRequiringDestinations = enabledTasks
            .Where(task => OutputProducingTasks.Contains(task))
            .OrderBy(task => task)
            .ToList();

        if (tasksRequiringDestinations.Count == 0)
        {
            return Gen.Constant((IReadOnlyDictionary<TaskKind, DestinationKey>)new Dictionary<TaskKind, DestinationKey>());
        }

        return Gen.Sequence(tasksRequiringDestinations.Select(_ => DestinationGen()))
            .Select(destinations => (IReadOnlyDictionary<TaskKind, DestinationKey>)tasksRequiringDestinations
                .Zip(destinations, (task, destination) => new KeyValuePair<TaskKind, DestinationKey>(task, destination))
                .ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private static Gen<DestinationKey> DestinationGen() =>
        Gen.OneOf(
            ChestDestinationGen().Select(destination => (DestinationKey)destination),
            Gen.Constant((DestinationKey)ShippingBinDestination.Instance),
            Gen.Constant((DestinationKey)MailDestination.Instance));

    private static Gen<ChestDestination> ChestDestinationGen() =>
        from locationName in Gen.Elements("Farm", "Greenhouse", "Barn", "Coop")
        from x in Gen.Choose(0, 50)
        from y in Gen.Choose(0, 50)
        select new ChestDestination(new ChestRef(locationName, new TileCoord(x, y)));

    private static Gen<GameDate> GameDateGen() =>
        from day in Gen.Choose(1, 28)
        from season in Gen.Elements(Season.Spring, Season.Summer, Season.Fall, Season.Winter)
        from year in Gen.Choose(1, 10)
        select new GameDate(day, season, year);

    private static Gen<EnergyTier> TierGen() =>
        Gen.Elements(EnergyTier.HalfDay, EnergyTier.FullDay, EnergyTier.Overtime);

    // A shuffled-but-complete category priority order.
    private static Gen<IReadOnlyList<TaskCategory>> CategoryPriorityGen() =>
        Gen.Constant(Enum.GetValues<TaskCategory>())
            .Select(categories => categories.OrderBy(_ => Guid.NewGuid()).ToList().AsReadOnly() as IReadOnlyList<TaskCategory>);

    private static ContractScopeSelection CanonicalizeScopeSelection(ContractScopeSelection selection) =>
        new(
            OutdoorZones: selection.OutdoorZones
                .Distinct()
                .OrderBy(zone => $"{zone.LocationName}|{zone.TopLeft.X}|{zone.TopLeft.Y}|{zone.BottomRight.X}|{zone.BottomRight.Y}", StringComparer.Ordinal)
                .ToList()
                .AsReadOnly(),
            AnimalBuildings: selection.AnimalBuildings
                .Distinct()
                .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                .ThenBy(building => building.Tier)
                .ToList()
                .AsReadOnly(),
            Greenhouse: selection.Greenhouse);

    private static ContractTermsSnapshot BuildTerms(
        ContractScopeSelection scopeSelection,
        IReadOnlySet<TaskKind> enabledTasks,
        EnergyTier tier) =>
        U18BuilderFactory.CreateTermsBuilder().BuildTerms(scopeSelection, enabledTasks, tier, ConfigDefaults.Build());
}
