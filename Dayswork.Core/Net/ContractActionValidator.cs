using Dayswork.Core.Domain;

namespace Dayswork.Core.Net;

/// <param name="ShiftRunning">A shift is live for this office — cancelling would strand a worker
/// mid-day, which the local menu already refuses.</param>
/// <param name="OwnerIsResolvable">The office's owner still exists as a farmer on this save. False
/// means the office is orphaned (a deleted farmhand slot), which is what makes it claimable.</param>
public sealed record ActionValidationContext(
    bool ProtocolMatches,
    bool Suspended,
    bool OfficeExists,
    long OfficeOwnerId,
    bool SenderIsHost,
    long SenderId,
    Contract? StoredContract,
    bool ShiftRunning,
    bool OwnerIsResolvable);

/// <summary>
/// The host's gate for the state changes that carry no draft. Pause and Cancel are deliberately
/// open to the host on ANY office (2.0 plan D4: an absent owner's worker has to be stoppable);
/// Resume, editing, and buying an upgrade are the owner's alone.
/// </summary>
public static class ContractActionValidator
{
    public static ContractRejectionCode? Validate(ContractActionRequestMessage request, ActionValidationContext context)
    {
        if (!context.ProtocolMatches)
            return ContractRejectionCode.VersionMismatch;

        if (context.Suspended)
            return ContractRejectionCode.Paused;

        // Buying an upgrade is per-player and touches no office at all.
        if (request.Action == ContractActionKind.PurchaseUpgrade)
            return null;

        if (!context.OfficeExists)
            return ContractRejectionCode.OfficeGone;

        var isOwner = context.SenderId == context.OfficeOwnerId;
        var mayAdminister = context.SenderIsHost
            && request.Action is ContractActionKind.Pause or ContractActionKind.Cancel or ContractActionKind.ClaimOffice;

        if (!isOwner && !mayAdminister)
            return ContractRejectionCode.NotOwner;

        switch (request.Action)
        {
            case ContractActionKind.ClaimOffice:
                // Claiming is for an office whose owner is gone; a live owner's office is theirs.
                return context.OwnerIsResolvable ? ContractRejectionCode.InvalidAction : null;

            case ContractActionKind.Pause:
                return context.StoredContract is { Status: ContractStatus.Active }
                    ? null
                    : ContractRejectionCode.InvalidAction;

            case ContractActionKind.Resume:
                return context.StoredContract is { Status: ContractStatus.Paused }
                    ? null
                    : ContractRejectionCode.InvalidAction;

            case ContractActionKind.Cancel:
                if (context.StoredContract is not { Status: ContractStatus.Active or ContractStatus.Paused })
                    return ContractRejectionCode.InvalidAction;
                return context.ShiftRunning ? ContractRejectionCode.InvalidAction : null;

            default:
                return ContractRejectionCode.InvalidAction;
        }
    }
}
