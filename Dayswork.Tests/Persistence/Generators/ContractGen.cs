using Dayswork.Core.Domain;
using FsCheck;

namespace Dayswork.Tests.Persistence.Generators;

public static class ContractGen
{
    // Builds valid contracts (non-null scope + terms) varying only the identity, schedule, and
    // status fields the store-state property tests exercise. Scope/terms content is fixed from the
    // canonical example contract — these tests care about the state machine, not pricing.
    public static Arbitrary<Contract> Contract() =>
        (from id in Arb.Generate<Guid>()
         from schedule in Gen.Elements(ContractSchedule.OneTime, ContractSchedule.Recurring)
         from status in Gen.Elements(ContractStatus.Active, ContractStatus.Paused, ContractStatus.Cancelled)
         from hireDate in GameDateGen()
         select U19PersistenceGen.CreateExampleCurrentSchemaContract() with
         {
             Id = new ContractId(id),
             Schedule = schedule,
             Status = status,
             HireDate = hireDate,
         })
        .ToArbitrary();

    private static Gen<GameDate> GameDateGen() =>
        from day in Gen.Choose(1, 28)
        from season in Gen.Elements(Season.Spring, Season.Summer, Season.Fall, Season.Winter)
        from year in Gen.Choose(1, 10)
        select new GameDate(day, season, year);
}
