using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

/// <summary>One office as the adoption sees it: its building id, and its owner (0 when the
/// building predates ownership or was placed by a path that bypasses <c>buildStructure</c>).</summary>
public readonly record struct OfficeIdentity(Guid OfficeId, long OwnerId);

/// <summary>What the 1.x → 2.0 adoption decided, for the caller to apply and report.</summary>
/// <param name="Adopted">The contract, bound to an office and an owner. Null when nothing was
/// adopted.</param>
/// <param name="Outcome">Why — drives the player-facing notice.</param>
public readonly record struct ContractAdoptionResult(Contract? Adopted, ContractAdoptionOutcome Outcome);

public enum ContractAdoptionOutcome
{
    /// <summary>No open contract in the old save data — nothing to do.</summary>
    NothingToAdopt,
    /// <summary>The contract was bound to the farm's only office.</summary>
    Adopted,
    /// <summary>There was an open contract but no office to bind it to (demolished after hiring).
    /// It is dropped: it cannot run, and there is no refund (2.0 plan D1).</summary>
    DroppedNoOffice,
    /// <summary>Several offices exist, so which one the contract belongs to is not knowable. Only
    /// reachable from a save made by the reverted multi-farmhand build; the contract is dropped
    /// rather than guessed at.</summary>
    DroppedAmbiguous,
}

/// <summary>
/// The 1.x → 2.0 migration, as a pure function. 1.x kept a flat contract list in the
/// <c>Dayswork.Contracts</c> save key and only ever ran the primary open contract; 2.0 keeps one
/// contract per office in that office's modData. Adoption picks that primary contract and binds it
/// to the farm's office. See the 2.0 plan's D6.
/// </summary>
public static class ContractAdoption
{
    /// <param name="legacyContracts">Everything loaded from the old save key, any status.</param>
    /// <param name="offices">The farm's offices. Order does not matter; only the count and, for a
    /// single office, its id and owner.</param>
    /// <param name="hostPlayerId">Fallback sponsor for an office whose owner is 0 — a building
    /// placed before ownership existed belongs to the host.</param>
    public static ContractAdoptionResult Adopt(
        IReadOnlyList<Contract> legacyContracts,
        IReadOnlyList<OfficeIdentity> offices,
        long hostPlayerId)
    {
        var primary = SelectPrimaryOpen(legacyContracts);
        if (primary is null)
            return new ContractAdoptionResult(null, ContractAdoptionOutcome.NothingToAdopt);

        if (offices.Count == 0)
            return new ContractAdoptionResult(null, ContractAdoptionOutcome.DroppedNoOffice);

        if (offices.Count > 1)
            return new ContractAdoptionResult(null, ContractAdoptionOutcome.DroppedAmbiguous);

        var office = offices[0];
        var adopted = primary with
        {
            OfficeId = office.OfficeId,
            OwnerId = office.OwnerId != 0 ? office.OwnerId : hostPlayerId,
        };

        return new ContractAdoptionResult(adopted, ContractAdoptionOutcome.Adopted);
    }

    /// <summary>
    /// 1.x's single reachable contract: Active wins over Paused, so residual open contracts left by
    /// the reverted multi-farmhand build can never shadow the one the shift engine actually ran.
    /// Everything else in the list is dropped — those extras were invisible in the 1.x UI and are
    /// not resurrected here.
    /// </summary>
    private static Contract? SelectPrimaryOpen(IReadOnlyList<Contract> contracts) =>
        contracts.FirstOrDefault(c => c.Status == ContractStatus.Active)
        ?? contracts.FirstOrDefault(c => c.Status == ContractStatus.Paused);
}
