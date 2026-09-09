namespace Dayswork.Tests.Net;

using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Tests.Persistence.Generators;
using Xunit;

/// <summary>
/// The host's commit gate, one test per rejection code. This is the whole of the trust boundary
/// between a client's draft and the world: everything else in the request path is transport.
/// </summary>
public class ContractCommitValidatorTests
{
    private const long OwnerId = 100L;
    private const long HostId = 1L;
    private const long StrangerId = 200L;

    private static readonly Contract StoredActive =
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with { OwnerId = OwnerId, Revision = 4 };

    private static ContractCommitRequestMessage NewHire() =>
        new() { RequestId = "r", ProtocolVersion = DaysworkProtocol.Version, OfficeId = "office", IsEdit = false };

    private static ContractCommitRequestMessage Edit(int expectedRevision) =>
        new()
        {
            RequestId = "r",
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = "office",
            IsEdit = true,
            ExpectedRevision = expectedRevision,
        };

    /// <summary>A context in which everything is fine, so each test can spoil exactly one thing.</summary>
    private static CommitValidationContext Valid(
        Contract? stored = null,
        bool protocolMatches = true,
        bool suspended = false,
        bool officeExists = true,
        long senderId = OwnerId,
        bool senderIsHost = false,
        bool draftIsValid = true,
        string[]? missingChests = null,
        string[]? missingMachines = null,
        string[]? missingPonds = null,
        int ownerMoney = 10_000,
        int upfrontPrice = 0,
        bool termsMatchQuote = true) =>
        new(
            ProtocolMatches: protocolMatches,
            Suspended: suspended,
            OfficeExists: officeExists,
            OfficeOwnerId: OwnerId,
            SenderIsHost: senderIsHost,
            SenderId: senderId,
            StoredContract: stored,
            DraftIsValid: draftIsValid,
            MissingChests: missingChests ?? Array.Empty<string>(),
            MissingMachines: missingMachines ?? Array.Empty<string>(),
            MissingPonds: missingPonds ?? Array.Empty<string>(),
            OwnerMoney: ownerMoney,
            UpfrontPrice: upfrontPrice,
            TermsMatchQuote: termsMatchQuote);

    [Fact]
    public void NewHireOnAFreeOffice_IsAccepted() =>
        Assert.Null(ContractCommitValidator.Validate(NewHire(), Valid()));

    [Fact]
    public void EditAtTheStoredRevision_IsAccepted() =>
        Assert.Null(ContractCommitValidator.Validate(Edit(4), Valid(stored: StoredActive)));

    [Fact]
    public void ProtocolMismatch_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.VersionMismatch,
            ContractCommitValidator.Validate(NewHire(), Valid(protocolMatches: false)));

    [Fact]
    public void Suspended_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.Paused,
            ContractCommitValidator.Validate(NewHire(), Valid(suspended: true)));

    [Fact]
    public void DemolishedOffice_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.OfficeGone,
            ContractCommitValidator.Validate(NewHire(), Valid(officeExists: false)));

    [Fact]
    public void SomeoneElsesOffice_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.NotOwner,
            ContractCommitValidator.Validate(NewHire(), Valid(senderId: StrangerId)));

    [Fact]
    public void TheHostMayCommitOnAnyOffice()
    {
        // The host is the authority; an admin commit is not an ownership violation. (The menus do
        // not offer it, but the loopback path is the host's own code and must not trip over this.)
        Assert.Null(ContractCommitValidator.Validate(NewHire(), Valid(senderId: HostId, senderIsHost: true)));
    }

    [Fact]
    public void EditAgainstAnOlderRevision_IsStale() =>
        Assert.Equal(
            ContractRejectionCode.Stale,
            ContractCommitValidator.Validate(Edit(3), Valid(stored: StoredActive)));

    [Fact]
    public void EditOfAContractThatIsGone_IsStale() =>
        Assert.Equal(
            ContractRejectionCode.Stale,
            ContractCommitValidator.Validate(Edit(4), Valid(stored: null)));

    [Fact]
    public void HiringOverAnOpenContract_IsStale()
    {
        // The client's view was behind — somebody already hired here. Stale rather than invalid,
        // because re-reading and trying again is exactly the right response.
        Assert.Equal(
            ContractRejectionCode.Stale,
            ContractCommitValidator.Validate(NewHire(), Valid(stored: StoredActive)));
    }

    [Fact]
    public void HiringOverACancelledContract_IsAccepted()
    {
        var cancelled = StoredActive with { Status = ContractStatus.Cancelled };

        Assert.Null(ContractCommitValidator.Validate(NewHire(), Valid(stored: cancelled)));
    }

    [Fact]
    public void HiringOverAnExecutedContract_IsAccepted()
    {
        var executed = StoredActive with { Status = ContractStatus.Executed };

        Assert.Null(ContractCommitValidator.Validate(NewHire(), Valid(stored: executed)));
    }

    [Fact]
    public void ADraftTheHostCannotPrice_IsInvalidScope() =>
        Assert.Equal(
            ContractRejectionCode.InvalidScope,
            ContractCommitValidator.Validate(NewHire(), Valid(draftIsValid: false)));

    [Fact]
    public void AMissingChest_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.ChestMissing,
            ContractCommitValidator.Validate(NewHire(), Valid(missingChests: new[] { "Farm(3,4)" })));

    [Fact]
    public void AMissingMachine_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.MachineMissing,
            ContractCommitValidator.Validate(NewHire(), Valid(missingMachines: new[] { "Farm(9,9)" })));

    [Fact]
    public void AMissingPond_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.PondMissing,
            ContractCommitValidator.Validate(NewHire(), Valid(missingPonds: new[] { "Farm(1,1)" })));

    [Fact]
    public void AnUnaffordableOneTimePrice_IsRejected() =>
        Assert.Equal(
            ContractRejectionCode.CannotAfford,
            ContractCommitValidator.Validate(NewHire(), Valid(ownerMoney: 100, upfrontPrice: 500)));

    [Fact]
    public void ExactlyEnoughGold_IsAccepted() =>
        Assert.Null(ContractCommitValidator.Validate(NewHire(), Valid(ownerMoney: 500, upfrontPrice: 500)));

    [Fact]
    public void ARecurringEditCostsNothingUpFrontAndIgnoresAnEmptyWallet() =>
        Assert.Null(ContractCommitValidator.Validate(Edit(4), Valid(stored: StoredActive, ownerMoney: 0)));

    [Fact]
    public void Dayswork2Review_R9_TermsThatAreNotTheOnesTheClientQuoted_AreRequoted() =>
        Assert.Equal(
            ContractRejectionCode.TermsChanged,
            ContractCommitValidator.Validate(NewHire(), Valid(termsMatchQuote: false, upfrontPrice: 500)));

    [Fact]
    public void Dayswork2Review_R9_ARequoteBeatsTheWalletCheck()
    {
        // A player quoted 500g by their own config file, facing a host that charges 1000g they
        // cannot afford. They are told the price moved — the thing they can act on — rather than
        // being called broke for a price they were never shown, and nothing is spent either way.
        var context = Valid(termsMatchQuote: false, ownerMoney: 500, upfrontPrice: 1000);

        Assert.Equal(ContractRejectionCode.TermsChanged, ContractCommitValidator.Validate(NewHire(), context));
    }

    [Fact]
    public void Dayswork2Review_R9_AnAcceptedRequoteCommitsOnTheSecondAttempt() =>
        Assert.Null(ContractCommitValidator.Validate(
            NewHire(),
            Valid(termsMatchQuote: true, ownerMoney: 1000, upfrontPrice: 1000)));

    [Fact]
    public void TheRootCauseIsReportedFirst()
    {
        // Several things are wrong at once. The player is told the office is gone, not that they
        // cannot afford a contract for a building that no longer exists.
        var context = Valid(
            officeExists: false,
            draftIsValid: false,
            missingChests: new[] { "Farm(3,4)" },
            ownerMoney: 0,
            upfrontPrice: 500);

        Assert.Equal(ContractRejectionCode.OfficeGone, ContractCommitValidator.Validate(NewHire(), context));
    }
}
