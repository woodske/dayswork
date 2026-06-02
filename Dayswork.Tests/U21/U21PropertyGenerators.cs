namespace Dayswork.Tests.U21;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using FsCheck;

public sealed record U21LedgerCase(int Capacity, WorkActionKind[] Actions);

public static class U21PropertyGenerators
{
    public static Arbitrary<U21LedgerCase> LedgerCase()
    {
        var defaults = ConfigDefaults.Build();
        var actions = Enum.GetValues<WorkActionKind>();

        var gen =
            from capacity in Gen.Choose(1, 50)
            from sequence in Gen.ArrayOf(Gen.Elements(actions))
            select new U21LedgerCase(capacity, sequence);

        return gen.ToArbitrary();
    }

    public static WorkerEnergyProfile BuildProfile(int capacity)
    {
        var defaults = ConfigDefaults.Build();
        var costs = new ReadOnlyDictionary<WorkActionKind, int>(
            defaults.WorkActionCosts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        return new WorkerEnergyProfile(capacity, costs);
    }

    public static ContractTermsSnapshot BuildTermsSnapshot(int capacity)
    {
        return new ContractTermsSnapshot(
            new PricingSnapshot(0),
            BuildProfile(capacity));
    }
}
