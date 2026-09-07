using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace Dayswork.Tests.Persistence;

// FsCheck invariants for OfficeContractStore state transitions (Pause / Resume / Cancel).
// The xUnit facts in OfficeContractStoreTests cover the specific behaviors; these properties
// verify the same invariants hold across the full space of randomly generated contracts.
public sealed class OfficeContractStoreStateTests
{
    private static readonly Guid Office = Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001");

    private static OfficeContractStore MakeStore() => new OfficeContractStore(_ => { });

    // Generated contracts predate ownership, so bind each to the one office under test.
    private static Contract AtOffice(Contract contract) => contract with { OfficeId = Office };

    // Pause(id) → Resume(id) is a round-trip: contract is Active after both operations.
    [Property]
    public Property PauseResume_IsRoundTrip() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
            contract =>
            {
                var store = MakeStore();
                store.Add(AtOffice(contract));
                store.Pause(Office);
                store.Resume(Office);
                return store.ForOffice(Office)!.Status == ContractStatus.Active;
            });

    // Pause(id) on an Active contract always sets status to Paused.
    [Property]
    public Property Pause_Active_SetsStatusPaused() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
            contract =>
            {
                var store = MakeStore();
                store.Add(AtOffice(contract));
                store.Pause(Office);
                return store.ForOffice(Office)!.Status == ContractStatus.Paused;
            });

    // Resume(id) on a Paused contract always sets status to Active.
    [Property]
    public Property Resume_Paused_SetsStatusActive() =>
        Prop.ForAll(
            ContractGen.Contract().Filter(c => c.Status == ContractStatus.Paused),
            contract =>
            {
                var store = MakeStore();
                store.Add(AtOffice(contract));
                store.Resume(Office);
                return store.ForOffice(Office)!.Status == ContractStatus.Active;
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
                store.Add(AtOffice(contract));
                store.Cancel(Office);
                var stored = store.ForOffice(Office)!;
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
                store.Add(AtOffice(contract));
                store.Pause(Office);
                store.Cancel(Office);
                return store.ForOffice(Office)!.Status == ContractStatus.Cancelled;
            });

    [Property(Arbitrary = new[] { typeof(PersistenceGenerators) }, MaxTest = 300)]
    public Property ReplaceTermsSnapshot_PreservesNonTermsFields() =>
        Prop.ForAll(
            PersistenceGenerators.CurrentSchemaContract(),
            PersistenceGenerators.TermsSnapshot(),
            (contract, replacementTerms) =>
            {
                var store = MakeStore();
                store.Add(AtOffice(contract));
                store.ReplaceTermsSnapshot(Office, replacementTerms);
                var stored = store.ForOffice(Office)!;
                // Revision is the store's own bookkeeping and advances on every commit; every
                // other field must survive a terms refresh untouched.
                return ContractStructuralComparer.ContractsEqual(
                    AtOffice(contract) with { TermsSnapshot = replacementTerms, Revision = stored.Revision },
                    stored);
            });
}
