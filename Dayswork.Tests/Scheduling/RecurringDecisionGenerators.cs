using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Tests.Generators;
using Dayswork.Tests.Persistence.Generators;
using FsCheck;

namespace Dayswork.Tests.Scheduling;

public static class RecurringDecisionGenerators
{
    public static Arbitrary<U23RecurringDecisionCase> DecisionCase()
    {
        var gen =
            from contract in PersistenceGenerators.CurrentSchemaContract().Generator
            from config in ConfigSnapshotGen.Snapshot().Generator
            from festivalToday in Arb.Generate<bool>()
            from availableGold in Gen.Choose(0, 100_000)
            let recurringContract = contract with
            {
                Schedule = ContractSchedule.Recurring,
                Status = ContractStatus.Active,
            }
            select new U23RecurringDecisionCase(recurringContract, config, festivalToday, availableGold);

        return Arb.From(gen);
    }

    public static Arbitrary<U23RecurringContractCase> SupportedRecurringContractCase()
    {
        var gen =
            from contract in PersistenceGenerators.CurrentSchemaContract().Generator
            from config in ConfigSnapshotGen.Snapshot().Generator
            let recurringContract = contract with
            {
                Schedule = ContractSchedule.Recurring,
                Status = ContractStatus.Active,
            }
            select new U23RecurringContractCase(recurringContract, config);

        return Arb.From(gen);
    }
}

public sealed record U23RecurringDecisionCase(
    Contract Contract,
    ConfigSnapshot Config,
    bool FestivalToday,
    int AvailableGold);

public sealed record U23RecurringContractCase(
    Contract Contract,
    ConfigSnapshot Config);
