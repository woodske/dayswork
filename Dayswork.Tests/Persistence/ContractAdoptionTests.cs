using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Xunit;

namespace Dayswork.Tests.Persistence;

/// <summary>
/// The 1.x → 2.0 migration (2.0 plan D6). 1.x kept a flat contract list in the save key and only
/// ever ran the primary open contract; 2.0 keeps one contract per office in that office's modData.
/// </summary>
public sealed class ContractAdoptionTests
{
    private const long Host = 76561190000000000L;
    private const long Guest = 76561190000000001L;
    private static readonly Guid OfficeA = Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001");
    private static readonly Guid OfficeB = Guid.Parse("0b0b0b0b-0000-0000-0000-000000000002");

    private static Contract Legacy(ContractStatus status) =>
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            Id = ContractId.New(),
            // A contract read from a v3 payload is unbound: no owner, no office.
            OwnerId = 0L,
            OfficeId = Guid.Empty,
            Status = status,
        };

    [Fact]
    public void NoContracts_AdoptsNothing()
    {
        var result = ContractAdoption.Adopt(
            Array.Empty<Contract>(),
            new[] { new OfficeIdentity(OfficeA, Host) },
            Host);

        Assert.Equal(ContractAdoptionOutcome.NothingToAdopt, result.Outcome);
        Assert.Null(result.Adopted);
    }

    [Fact]
    public void NoOpenContract_AdoptsNothing()
    {
        var result = ContractAdoption.Adopt(
            new[] { Legacy(ContractStatus.Executed), Legacy(ContractStatus.Cancelled) },
            new[] { new OfficeIdentity(OfficeA, Host) },
            Host);

        Assert.Equal(ContractAdoptionOutcome.NothingToAdopt, result.Outcome);
    }

    [Fact]
    public void SingleOffice_BindsTheContractToItAndToItsOwner()
    {
        var contract = Legacy(ContractStatus.Active);

        var result = ContractAdoption.Adopt(
            new[] { contract },
            new[] { new OfficeIdentity(OfficeA, Guest) },
            Host);

        Assert.Equal(ContractAdoptionOutcome.Adopted, result.Outcome);
        Assert.Equal(contract.Id, result.Adopted!.Id);
        Assert.Equal(OfficeA, result.Adopted.OfficeId);
        Assert.Equal(Guest, result.Adopted.OwnerId);
    }

    [Fact]
    public void OfficeOwnerZero_FallsBackToTheHost()
    {
        // Building.owner is set by buildStructure, so a 0 means the building predates ownership
        // (or was placed by a path that bypasses it). Those belong to the host.
        var result = ContractAdoption.Adopt(
            new[] { Legacy(ContractStatus.Active) },
            new[] { new OfficeIdentity(OfficeA, 0L) },
            Host);

        Assert.Equal(Host, result.Adopted!.OwnerId);
    }

    [Fact]
    public void ActiveWinsOverPaused_AndResidualExtrasAreDropped()
    {
        // Saves from the reverted multi-farmhand build can hold several open contracts. 1.x only
        // ever ran one of them; the extras were invisible in its UI and are not resurrected here.
        var paused = Legacy(ContractStatus.Paused);
        var active = Legacy(ContractStatus.Active);
        var extra = Legacy(ContractStatus.Active);

        var result = ContractAdoption.Adopt(
            new[] { paused, active, extra },
            new[] { new OfficeIdentity(OfficeA, Host) },
            Host);

        Assert.Equal(active.Id, result.Adopted!.Id);
    }

    [Fact]
    public void PausedContractIsAdoptedWhenNoActiveOneExists()
    {
        var paused = Legacy(ContractStatus.Paused);

        var result = ContractAdoption.Adopt(
            new[] { Legacy(ContractStatus.Executed), paused },
            new[] { new OfficeIdentity(OfficeA, Host) },
            Host);

        Assert.Equal(paused.Id, result.Adopted!.Id);
        Assert.Equal(ContractStatus.Paused, result.Adopted.Status);
    }

    [Fact]
    public void NoOffice_DropsTheContract()
    {
        // The office was demolished after hiring: the contract cannot run and there is no refund.
        var result = ContractAdoption.Adopt(
            new[] { Legacy(ContractStatus.Active) },
            Array.Empty<OfficeIdentity>(),
            Host);

        Assert.Equal(ContractAdoptionOutcome.DroppedNoOffice, result.Outcome);
        Assert.Null(result.Adopted);
    }

    [Fact]
    public void SeveralOffices_DropsTheContractRatherThanGuessing()
    {
        // 1.x capped the farm at one office, so this only happens on a save from the reverted
        // multi-farmhand build. Which office the contract belonged to is not recoverable.
        var result = ContractAdoption.Adopt(
            new[] { Legacy(ContractStatus.Active) },
            new[] { new OfficeIdentity(OfficeA, Host), new OfficeIdentity(OfficeB, Host) },
            Host);

        Assert.Equal(ContractAdoptionOutcome.DroppedAmbiguous, result.Outcome);
        Assert.Null(result.Adopted);
    }

    [Fact]
    public void AdoptionPreservesEverythingElseAboutTheContract()
    {
        var contract = Legacy(ContractStatus.Active);

        var adopted = ContractAdoption
            .Adopt(new[] { contract }, new[] { new OfficeIdentity(OfficeA, Host) }, Host)
            .Adopted!;

        Assert.True(ContractStructuralComparer.ContractsEqual(
            contract with { OfficeId = OfficeA, OwnerId = Host },
            adopted));
    }
}
