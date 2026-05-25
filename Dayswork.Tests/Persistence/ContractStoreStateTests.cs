using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace Dayswork.Tests.Persistence;

// FsCheck PBT-03 invariants for ContractStore state transitions (Pause / Resume / Cancel).
// The xUnit facts in ContractStoreTests cover the specific behaviors; these properties
// verify the same invariants hold across the full space of randomly generated contracts.
public sealed class ContractStoreStateTests
{
    private static ContractStore MakeStore() => new ContractStore(_ => { });

    // Pause(id) → Resume(id) is a round-trip: contract is Active after both operations.
    [Property]
    public Property PauseResume_IsRoundTrip() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
            contract =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.Pause(contract.Id);
                store.Resume(contract.Id);
                return store.Get(contract.Id).Status == ContractStatus.Active;
            });

    // Pause(id) on an Active contract always sets status to Paused.
    [Property]
    public Property Pause_Active_SetsStatusPaused() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
            contract =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.Pause(contract.Id);
                return store.Get(contract.Id).Status == ContractStatus.Paused;
            });

    // Resume(id) on a Paused contract always sets status to Active.
    [Property]
    public Property Resume_Paused_SetsStatusActive() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Paused),
            contract =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.Resume(contract.Id);
                return store.Get(contract.Id).Status == ContractStatus.Active;
            });

    // Cancel(id) on an Active or Paused contract always sets status to Cancelled
    // and the contract remains accessible (not removed from store).
    [Property]
    public Property Cancel_ManageableContract_SetsStatusCancelled() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c =>
                c.Status == ContractStatus.Active || c.Status == ContractStatus.Paused),
            contract =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.Cancel(contract.Id);
                var stored = store.Get(contract.Id);
                return stored.Status == ContractStatus.Cancelled && stored.Id == contract.Id;
            });

    // Pause followed by Cancel leaves the contract in Cancelled state.
    [Property]
    public Property PauseThenCancel_LeavesStatusCancelled() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
            contract =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.Pause(contract.Id);
                store.Cancel(contract.Id);
                return store.Get(contract.Id).Status == ContractStatus.Cancelled;
            });

    [Property(Arbitrary = new[] { typeof(U19PersistenceGen) }, MaxTest = 300)]
    public Property ReplaceTermsSnapshot_PreservesNonTermsFields() =>
        Prop.ForAll(
            U19PersistenceGen.CurrentSchemaContract(),
            U19PersistenceGen.TermsSnapshot(),
            (contract, replacementTerms) =>
            {
                var store = MakeStore();
                store.Add(contract);
                store.ReplaceTermsSnapshot(contract.Id, replacementTerms);
                var stored = store.Get(contract.Id);
                return ContractStructuralComparer.ContractsEqual(
                    contract with { TermsSnapshot = replacementTerms },
                    stored);
            });
}
