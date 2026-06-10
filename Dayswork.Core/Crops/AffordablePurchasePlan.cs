namespace Dayswork.Core.Crops;

/// <summary>
/// The wallet-clamped subset of a <see cref="ShiftPurchaseManifest"/> the farmhand can actually
/// afford this shift. When the wallet covers the whole manifest the plan
/// equals the manifest; otherwise it is the largest affordable prefix and <see cref="Shortfall"/>
/// is set so the runtime emits a single insufficient-funds notice.
/// </summary>
public sealed record AffordablePurchasePlan
{
    public static AffordablePurchasePlan Empty { get; } =
        new(Array.Empty<StorePurchaseGroup>(), shortfall: false);

    public IReadOnlyList<StorePurchaseGroup> Groups { get; }
    public bool Shortfall { get; }

    public AffordablePurchasePlan(IReadOnlyList<StorePurchaseGroup>? groups, bool shortfall)
    {
        Groups = (groups ?? Array.Empty<StorePurchaseGroup>())
            .Where(group => group.Lines.Count > 0)
            .ToList()
            .AsReadOnly();
        Shortfall = shortfall;
    }

    public bool HasPurchases => Groups.Count > 0;

    public int TotalCost => Groups.Sum(group => group.TotalCost);
}
