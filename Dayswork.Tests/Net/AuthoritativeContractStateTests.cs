using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Newtonsoft.Json;
using Xunit;

namespace Dayswork.Tests.Net;

/// <summary>
/// R7: the host stamps every commit/action answer with what the office actually holds, and the
/// client folds that into its read cache before the menu redraws. These cover the round trip that
/// carries it and the store update at the far end — the two places where an accepted Pause could
/// otherwise still redraw as Active.
/// </summary>
public sealed class AuthoritativeContractStateTests
{
    private static readonly Guid Office = Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001");

    private readonly SaveDataSerializer _serializer = new(_ => { });

    private static Contract MakeContract(ContractStatus status) =>
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            Id = ContractId.New(),
            OfficeId = Office,
            OwnerId = 42L,
            Status = status,
            Revision = 7,
        };

    private static T RoundTrip<T>(T message) =>
        JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(message))!;

    /// <summary>What <c>ContractRequestClient.ApplyAuthoritativeState</c> does once the tracker has
    /// accepted a state: the same two steps, without the SMAPI context a client needs.</summary>
    private void Apply(OfficeContractStore store, AuthoritativeContractState state)
    {
        Assert.True(Guid.TryParse(state.OfficeId, out var officeId));
        store.HydrateOffice(
            officeId,
            state is { OfficeExists: true, HasContract: true }
                ? _serializer.DeserializeOne(state.ContractJson)
                : null);
    }

    private AuthoritativeContractState StateFor(Contract? contract, bool officeExists = true, bool shiftRunning = false) =>
        new()
        {
            OfficeId = Office.ToString("N"),
            Sequence = 1,
            OfficeExists = officeExists,
            HasContract = contract is not null,
            ContractJson = contract is null ? "" : _serializer.SerializeOne(contract, "2.0.0"),
            Revision = contract?.Revision ?? -1,
            ShiftRunning = shiftRunning,
        };

    [Fact]
    public void Dayswork2Review_R7_ActionResponseCarriesTheOfficeStateOverTheWire()
    {
        var contract = MakeContract(ContractStatus.Paused);
        var result = RoundTrip(new ContractActionResponseMessage
        {
            RequestId = "r1",
            Action = ContractActionKind.Pause,
            Accepted = true,
            State = StateFor(contract, shiftRunning: true),
        });

        Assert.NotNull(result.State);
        Assert.Equal(Office.ToString("N"), result.State!.OfficeId);
        Assert.Equal(1, result.State.Sequence);
        Assert.True(result.State.HasContract);
        Assert.True(result.State.ShiftRunning);
        Assert.Equal(7, result.State.Revision);
        Assert.Equal(ContractStatus.Paused, _serializer.DeserializeOne(result.State.ContractJson)!.Status);
    }

    [Fact]
    public void Dayswork2Review_R7_CommitResponseCarriesTheOfficeStateEvenWhenRejected()
    {
        var result = RoundTrip(new ContractCommitResponseMessage
        {
            RequestId = "r2",
            Accepted = false,
            Code = ContractRejectionCode.Stale,
            State = StateFor(MakeContract(ContractStatus.Active)),
        });

        Assert.False(result.Accepted);
        Assert.Equal(ContractRejectionCode.Stale, result.Code);
        Assert.NotNull(result.State);
        Assert.Equal(7, result.State!.Revision);
    }

    [Fact]
    public void Dayswork2Review_R7_ApplyingAnAcceptedPauseMakesTheClientReadItAsPaused()
    {
        var store = new OfficeContractStore(_ => { });
        store.HydrateOffice(Office, MakeContract(ContractStatus.Active));

        Apply(store, RoundTrip(StateFor(MakeContract(ContractStatus.Paused))));

        Assert.Equal(ContractStatus.Paused, store.ForOffice(Office)!.Status);
    }

    [Fact]
    public void Dayswork2Review_R7_ApplyingACancelledOfficeClearsTheClientsCard()
    {
        var store = new OfficeContractStore(_ => { });
        store.HydrateOffice(Office, MakeContract(ContractStatus.Active));

        // Cancel keeps the contract with a Cancelled status; the office reads as free to hire.
        Apply(store, RoundTrip(StateFor(MakeContract(ContractStatus.Cancelled))));

        Assert.False(store.HasOpenContract(Office));
    }

    [Fact]
    public void Dayswork2Review_R7_ApplyingADemolishedOfficeDropsItsContractEntirely()
    {
        var store = new OfficeContractStore(_ => { });
        store.HydrateOffice(Office, MakeContract(ContractStatus.Active));

        Apply(store, RoundTrip(StateFor(contract: null, officeExists: false)));

        Assert.Null(store.ForOffice(Office));
    }

    [Fact]
    public void Dayswork2Review_R7_OutOfOrderAnswersLeaveTheNewerStateInPlace()
    {
        var store = new OfficeContractStore(_ => { });
        var tracker = new AuthoritativeStateTracker();

        var paused = StateFor(MakeContract(ContractStatus.Paused));
        paused.Sequence = 2;
        var active = StateFor(MakeContract(ContractStatus.Active));
        active.Sequence = 1;

        if (tracker.TryAccept(paused))
            Apply(store, paused);

        // The older answer arrives afterwards — a replayed request id, or two answers crossing.
        if (tracker.TryAccept(active))
            Apply(store, active);

        Assert.Equal(ContractStatus.Paused, store.ForOffice(Office)!.Status);
    }
}
