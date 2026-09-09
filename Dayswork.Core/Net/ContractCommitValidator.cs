using Dayswork.Core.Domain;

namespace Dayswork.Core.Net;

/// <summary>
/// Everything the host knows about a commit request once it has re-resolved the request against
/// its own world. Gathering the facts is the adapter's job (it needs SMAPI/Stardew); deciding is
/// this file's, so the decision is unit-testable per rejection code.
/// </summary>
/// <param name="ProtocolMatches">Sender's protocol version equals the host's.</param>
/// <param name="Suspended">Dayswork is suspended on the host (incompatible peer connected).</param>
/// <param name="OfficeExists">The office building is still on the farm.</param>
/// <param name="OfficeOwnerId">Its <c>Building.owner</c>. Ignored when the office is gone.</param>
/// <param name="SenderIsHost">The request came from the host itself (the loopback path) — the host
/// may act on any office.</param>
/// <param name="SenderId">The requesting player's multiplayer id.</param>
/// <param name="StoredContract">The office's contract as the host holds it, or null when the
/// office has none.</param>
/// <param name="DraftIsValid">The draft passed the host's own confirm gate (the same
/// <c>ContractTermsBuilder.BuildPreview</c> check the local flow runs).</param>
/// <param name="MissingChests">Chest references the host could not resolve.</param>
/// <param name="MissingMachines">Machine references the host could not resolve.</param>
/// <param name="MissingPonds">Fish-pond references the host could not resolve.</param>
/// <param name="OwnerMoney">The owner's wallet balance.</param>
/// <param name="UpfrontPrice">What committing costs now — the one-time price for a new one-time
/// contract, zero otherwise.</param>
/// <param name="TermsMatchQuote">The terms carried by the submitted draft — the ones the player
/// reviewed — are the terms the host has just computed for itself. False when the host's pricing or
/// energy configuration says something the client's preview did not (R9).</param>
public sealed record CommitValidationContext(
    bool ProtocolMatches,
    bool Suspended,
    bool OfficeExists,
    long OfficeOwnerId,
    bool SenderIsHost,
    long SenderId,
    Contract? StoredContract,
    bool DraftIsValid,
    IReadOnlyCollection<string> MissingChests,
    IReadOnlyCollection<string> MissingMachines,
    IReadOnlyCollection<string> MissingPonds,
    int OwnerMoney,
    int UpfrontPrice,
    bool TermsMatchQuote);

/// <summary>
/// The host's commit gate. Pure: the same function decides for a remote client's request and for
/// the host's own loopback commit, so there is one answer to "may this contract be written" rather
/// than a menu-side check and a network-side check that can drift apart.
/// </summary>
public static class ContractCommitValidator
{
    /// <summary>The rejection code, or null when the commit may proceed. Checks run cheapest and
    /// most-fundamental first so the reported reason is the root one: a commit for a demolished
    /// office reads OfficeGone, not CannotAfford.</summary>
    public static ContractRejectionCode? Validate(ContractCommitRequestMessage request, CommitValidationContext context)
    {
        if (!context.ProtocolMatches)
            return ContractRejectionCode.VersionMismatch;

        if (context.Suspended)
            return ContractRejectionCode.Paused;

        if (!context.OfficeExists)
            return ContractRejectionCode.OfficeGone;

        if (!context.SenderIsHost && context.SenderId != context.OfficeOwnerId)
            return ContractRejectionCode.NotOwner;

        if (request.IsEdit)
        {
            // Editing something that is no longer there, or that has moved on since the draft was
            // built, is stale rather than invalid: the client re-reads and tries again.
            if (context.StoredContract is null)
                return ContractRejectionCode.Stale;
            if (context.StoredContract.Revision != request.ExpectedRevision)
                return ContractRejectionCode.Stale;
        }
        else if (IsOpen(context.StoredContract))
        {
            // Hiring over an office that already has an open contract — the client's view was
            // behind, so this is the same "re-read and retry" case.
            return ContractRejectionCode.Stale;
        }

        if (!context.DraftIsValid)
            return ContractRejectionCode.InvalidScope;

        if (context.MissingChests.Count > 0)
            return ContractRejectionCode.ChestMissing;
        if (context.MissingMachines.Count > 0)
            return ContractRejectionCode.MachineMissing;
        if (context.MissingPonds.Count > 0)
            return ContractRejectionCode.PondMissing;

        // Before the wallet check on purpose: a player quoted 500g and about to be charged 1000g
        // should be told the price moved, not that they are poor — and either way nothing is spent
        // until they have confirmed the host's own figure.
        if (!context.TermsMatchQuote)
            return ContractRejectionCode.TermsChanged;

        if (context.UpfrontPrice > 0 && context.OwnerMoney < context.UpfrontPrice)
            return ContractRejectionCode.CannotAfford;

        return null;
    }

    private static bool IsOpen(Contract? contract) =>
        contract is { Status: ContractStatus.Active or ContractStatus.Paused };
}
