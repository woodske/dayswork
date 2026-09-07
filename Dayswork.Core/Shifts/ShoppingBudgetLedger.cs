using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Per-day ledger of gold each concurrent shift has committed to spending on a managed-crop
/// shopping trip. The player's wallet is shared by every farmhand, but money is only debited when a
/// worker reaches the store counter — so a second worker that checks the raw wallet mid-trip would
/// see gold the first worker is already on its way to spend, depart too, and arrive to an empty
/// wallet. Workers reserve their planned spend when departing and consult
/// <see cref="ReservedByOthers"/> before deciding whether to travel, so only workers who can
/// actually afford seeds make the trip.
///
/// Created fresh each morning (held by the fleet's per-day state) and single-threaded by design —
/// the fleet fans events out sequentially, mirroring <see cref="WorkClaimRegistry"/>.
/// </summary>
public sealed class ShoppingBudgetLedger
{
    private readonly Dictionary<ContractId, int> _reservations = new();

    /// <summary>Records (overwriting any prior reservation) the gold <paramref name="owner"/> intends
    /// to spend this shift. Non-positive amounts clear the reservation.</summary>
    public void Reserve(ContractId owner, int amount)
    {
        if (amount <= 0)
        {
            _reservations.Remove(owner);
            return;
        }

        _reservations[owner] = amount;
    }

    /// <summary>Drops <paramref name="owner"/>'s reservation (the trip has returned or was aborted).</summary>
    public void Release(ContractId owner) => _reservations.Remove(owner);

    /// <summary>Total gold reserved by every contract other than <paramref name="owner"/>.</summary>
    public int ReservedByOthers(ContractId owner)
    {
        var total = 0;
        foreach (var (contract, amount) in _reservations)
        {
            if (contract != owner)
                total += amount;
        }

        return total;
    }
}
