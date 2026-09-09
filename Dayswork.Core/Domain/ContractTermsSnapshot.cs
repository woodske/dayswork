namespace Dayswork.Core.Domain;

using Dayswork.Core.Energy;

public sealed record ContractTermsSnapshot(PricingSnapshot Pricing, WorkerEnergyProfile Energy)
{
    /// <summary>
    /// Whether two sets of terms are the same bargain. Record equality cannot answer this — the
    /// energy profile's action costs are an <see cref="IReadOnlyDictionary{TKey,TValue}"/>, which
    /// compares by reference — and the host needs a real answer before it charges anyone: the
    /// terms a client reviewed and the terms the host just computed have to agree, or the player
    /// is quoted one price and billed another (R9).
    /// </summary>
    public static bool SameTerms(ContractTermsSnapshot? left, ContractTermsSnapshot? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;

        if (left.Pricing.TotalPrice != right.Pricing.TotalPrice)
            return false;
        if (left.Energy.DailyCapacity != right.Energy.DailyCapacity)
            return false;
        if (left.Energy.ActionCosts.Count != right.Energy.ActionCosts.Count)
            return false;

        return left.Energy.ActionCosts.All(cost =>
            right.Energy.ActionCosts.TryGetValue(cost.Key, out var value) && value == cost.Value);
    }
}
