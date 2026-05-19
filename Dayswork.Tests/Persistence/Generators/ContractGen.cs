using Dayswork.Core.Domain;
using Dayswork.Tests.Generators;
using FsCheck;

namespace Dayswork.Tests.Persistence.Generators;

public static class ContractGen
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

    public static Arbitrary<Contract> Contract() =>
        (from id in ContractIdGen()
         from enabledTasks in EnabledTasksGen()
         from zones in ZonesGen()
         from taskDestinations in TaskDestinationsGen()
         from schedule in Gen.Elements(ContractSchedule.OneTime, ContractSchedule.Recurring)
         from status in Gen.Elements(ContractStatus.Active, ContractStatus.Paused, ContractStatus.Cancelled)
         from hireDate in GameDateGen()
         from financial in FinancialGen()
         select new Core.Domain.Contract(
             Id: id,
             EnabledTasks: enabledTasks,
             Zones: zones,
             TaskDestinations: taskDestinations,
             Schedule: schedule,
             Status: status,
             HireDate: hireDate,
             DepositAmount: financial.deposit,
             HourlyRate: financial.rate))
        .ToArbitrary();

    private static Gen<ContractId> ContractIdGen() =>
        Arb.Generate<Guid>().Select(g => new ContractId(g));

    private static Gen<IReadOnlySet<TaskKind>> EnabledTasksGen() =>
        Gen.SubListOf(Enum.GetValues<TaskKind>())
           .Where(l => l.Count > 0)
           .Select(l => (IReadOnlySet<TaskKind>)l.ToHashSet());

    private static Gen<IReadOnlyList<Zone>> ZonesGen() =>
        ZoneGen.Zone().Generator
               .NonEmptyListOf()
               .Select(l => (IReadOnlyList<Zone>)l.ToList());

    private static Gen<IReadOnlyDictionary<TaskKind, DestinationKey>> TaskDestinationsGen() =>
        Gen.SubListOf(OutputProducingTasks)
           .SelectMany(keys => keys.Count == 0
               ? Gen.Constant((IReadOnlyDictionary<TaskKind, DestinationKey>)new Dictionary<TaskKind, DestinationKey>())
               : Gen.Sequence(keys.Select(k => DestinationGen().Select(d => (k, d))))
                    .Select(pairs => (IReadOnlyDictionary<TaskKind, DestinationKey>)pairs.ToDictionary(p => p.k, p => p.d)));

    private static Gen<DestinationKey> DestinationGen() =>
        Gen.OneOf(
            ChestDestinationGen().Select(d => (DestinationKey)d),
            Gen.Constant((DestinationKey)ShippingBinDestination.Instance),
            Gen.Constant((DestinationKey)MailDestination.Instance));

    private static Gen<ChestDestination> ChestDestinationGen() =>
        from loc in Gen.Elements("Farm", "Greenhouse", "Barn", "Coop")
        from x in Gen.Choose(0, 50)
        from y in Gen.Choose(0, 50)
        select new ChestDestination(new ChestRef(loc, new TileCoord(x, y)));

    private static Gen<GameDate> GameDateGen() =>
        from day in Gen.Choose(1, 28)
        from season in Gen.Elements(Season.Spring, Season.Summer, Season.Fall, Season.Winter)
        from year in Gen.Choose(1, 10)
        select new GameDate(day, season, year);

    private static Gen<(int deposit, int rate)> FinancialGen() =>
        from deposit in Gen.Choose(0, 10_000)
        from rate in Gen.Choose(50, 500)
        select (deposit, rate);
}
