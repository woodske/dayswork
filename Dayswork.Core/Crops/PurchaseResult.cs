namespace Dayswork.Core.Crops;

/// <summary>Outcome of one purchase line at the counter (U-MC-06), used to pace HUD notices.</summary>
public enum PurchaseOutcomeKind
{
    Full,
    Partial,
    Insufficient,
    OutOfStock,
    BindFailed,
}

/// <summary>Per-line result of a headless purchase: what was requested, bought, and why.</summary>
public sealed record PurchaseLineOutcome(
    Store Store,
    string ItemId,
    string DisplayName,
    int RequestedQty,
    int BoughtQty,
    int UnitCost,
    PurchaseOutcomeKind Kind)
{
    public int SpentGold => Math.Max(0, BoughtQty) * Math.Max(0, UnitCost);
}

/// <summary>Aggregate result of a headless store transaction (M-26 ShopPurchaseService).</summary>
public sealed record PurchaseResult
{
    public static PurchaseResult BindFailure { get; } =
        new(Array.Empty<PurchaseLineOutcome>(), bindFailed: true);

    public IReadOnlyList<PurchaseLineOutcome> Outcomes { get; }
    public bool BindFailed { get; }

    public PurchaseResult(IReadOnlyList<PurchaseLineOutcome>? outcomes, bool bindFailed)
    {
        Outcomes = (outcomes ?? Array.Empty<PurchaseLineOutcome>()).ToList().AsReadOnly();
        BindFailed = bindFailed;
    }

    public int TotalSpentGold => Outcomes.Sum(outcome => outcome.SpentGold);

    public bool AnyShortfall =>
        Outcomes.Any(outcome => outcome.Kind is PurchaseOutcomeKind.Partial or PurchaseOutcomeKind.Insufficient);
}
