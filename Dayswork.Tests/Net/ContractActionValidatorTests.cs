namespace Dayswork.Tests.Net;

using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Tests.Persistence.Generators;
using Xunit;

/// <summary>
/// The gate for the actions that carry no draft. The interesting rule here is the asymmetry the
/// 2.0 plan asks for (D4): the host may <b>stop</b> anyone's farmhand, because an absent owner's
/// worker still has to be stoppable, but only the owner may start one again.
/// </summary>
public class ContractActionValidatorTests
{
    private const long OwnerId = 100L;
    private const long HostId = 1L;
    private const long StrangerId = 200L;

    private static readonly Contract Active =
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with { OwnerId = OwnerId };

    private static readonly Contract Paused = Active with { Status = ContractStatus.Paused };

    private static ContractActionRequestMessage Request(ContractActionKind action, string upgradeKind = "") =>
        new()
        {
            RequestId = "r",
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = "office",
            Action = action,
            UpgradeKind = upgradeKind,
        };

    private static ActionValidationContext Context(
        Contract? stored,
        long senderId = OwnerId,
        bool senderIsHost = false,
        bool officeExists = true,
        bool shiftRunning = false,
        bool ownerIsResolvable = true,
        bool protocolMatches = true,
        bool suspended = false) =>
        new(
            ProtocolMatches: protocolMatches,
            Suspended: suspended,
            OfficeExists: officeExists,
            OfficeOwnerId: OwnerId,
            SenderIsHost: senderIsHost,
            SenderId: senderId,
            StoredContract: stored,
            ShiftRunning: shiftRunning,
            OwnerIsResolvable: ownerIsResolvable);

    [Fact]
    public void OwnerMayPauseAnActiveContract() =>
        Assert.Null(ContractActionValidator.Validate(Request(ContractActionKind.Pause), Context(Active)));

    [Fact]
    public void OwnerMayResumeAPausedContract() =>
        Assert.Null(ContractActionValidator.Validate(Request(ContractActionKind.Resume), Context(Paused)));

    [Fact]
    public void HostMayPauseSomeoneElsesContract() =>
        Assert.Null(ContractActionValidator.Validate(
            Request(ContractActionKind.Pause),
            Context(Active, senderId: HostId, senderIsHost: true)));

    [Fact]
    public void HostMayCancelSomeoneElsesContract() =>
        Assert.Null(ContractActionValidator.Validate(
            Request(ContractActionKind.Cancel),
            Context(Active, senderId: HostId, senderIsHost: true)));

    [Fact]
    public void HostMayNotResumeSomeoneElsesContract() =>
        Assert.Equal(
            ContractRejectionCode.NotOwner,
            ContractActionValidator.Validate(
                Request(ContractActionKind.Resume),
                Context(Paused, senderId: HostId, senderIsHost: true)));

    [Fact]
    public void AStrangerMayDoNothing() =>
        Assert.Equal(
            ContractRejectionCode.NotOwner,
            ContractActionValidator.Validate(Request(ContractActionKind.Pause), Context(Active, senderId: StrangerId)));

    [Fact]
    public void PausingAnAlreadyPausedContract_IsInvalid() =>
        Assert.Equal(
            ContractRejectionCode.InvalidAction,
            ContractActionValidator.Validate(Request(ContractActionKind.Pause), Context(Paused)));

    [Fact]
    public void ResumingAnActiveContract_IsInvalid() =>
        Assert.Equal(
            ContractRejectionCode.InvalidAction,
            ContractActionValidator.Validate(Request(ContractActionKind.Resume), Context(Active)));

    [Fact]
    public void CancellingWhileTheShiftIsRunning_IsInvalid() =>
        Assert.Equal(
            ContractRejectionCode.InvalidAction,
            ContractActionValidator.Validate(Request(ContractActionKind.Cancel), Context(Active, shiftRunning: true)));

    [Fact]
    public void CancellingWhenThereIsNoContract_IsInvalid() =>
        Assert.Equal(
            ContractRejectionCode.InvalidAction,
            ContractActionValidator.Validate(Request(ContractActionKind.Cancel), Context(stored: null)));

    [Fact]
    public void ClaimingAnOrphanedOffice_IsAllowedForTheHost() =>
        Assert.Null(ContractActionValidator.Validate(
            Request(ContractActionKind.ClaimOffice),
            Context(Active, senderId: HostId, senderIsHost: true, ownerIsResolvable: false)));

    [Fact]
    public void ClaimingAnOfficeWhoseOwnerStillExists_IsInvalid() =>
        Assert.Equal(
            ContractRejectionCode.InvalidAction,
            ContractActionValidator.Validate(
                Request(ContractActionKind.ClaimOffice),
                Context(Active, senderId: HostId, senderIsHost: true, ownerIsResolvable: true)));

    [Fact]
    public void AGuestMayNotClaimAnOrphanedOffice() =>
        Assert.Equal(
            ContractRejectionCode.NotOwner,
            ContractActionValidator.Validate(
                Request(ContractActionKind.ClaimOffice),
                Context(Active, senderId: StrangerId, ownerIsResolvable: false)));

    [Fact]
    public void BuyingAnUpgradeTouchesNoOfficeAndIsAlwaysAllowed()
    {
        // Upgrades are per player and bought from the buyer's own wallet, so the office the request
        // happens to name is irrelevant — even a demolished one.
        Assert.Null(ContractActionValidator.Validate(
            Request(ContractActionKind.PurchaseUpgrade, "Speed"),
            Context(stored: null, senderId: StrangerId, officeExists: false)));
    }

    [Fact]
    public void ProtocolMismatchBeatsEverything() =>
        Assert.Equal(
            ContractRejectionCode.VersionMismatch,
            ContractActionValidator.Validate(
                Request(ContractActionKind.PurchaseUpgrade, "Speed"),
                Context(Active, protocolMatches: false)));

    [Fact]
    public void SuspensionBlocksEveryAction() =>
        Assert.Equal(
            ContractRejectionCode.Paused,
            ContractActionValidator.Validate(Request(ContractActionKind.Pause), Context(Active, suspended: true)));
}
