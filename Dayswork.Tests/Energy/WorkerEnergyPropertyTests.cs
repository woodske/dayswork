namespace Dayswork.Tests.Energy;

using Dayswork.Core.Energy;
using FsCheck.Xunit;

public sealed class WorkerEnergyPropertyTests
{
    [Property(Arbitrary = new[] { typeof(EnergyGenerators) }, MaxTest = 200)]
    public bool Energy_sequences_stay_within_bounds_and_never_regain_new_unit_privilege(U21LedgerCase input)
    {
        var ledger = new WorkerEnergyLedger();
        var profile = EnergyGenerators.BuildProfile(input.Capacity);
        var state = ledger.StartShift(profile);
        var hasLostStartPrivilege = false;

        foreach (var action in input.Actions)
        {
            var result = ledger.ApplyActionCost(state, action);
            state = result.State;

            if (!state.CanStartNewWorkUnit)
                hasLostStartPrivilege = true;

            if (state.RemainingEnergy < 0 || state.RemainingEnergy > state.Capacity)
                return false;

            if (hasLostStartPrivilege && state.CanStartNewWorkUnit)
                return false;
        }

        return true;
    }
}
