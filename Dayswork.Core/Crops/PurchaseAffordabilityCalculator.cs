namespace Dayswork.Core.Crops;

/// <summary>
/// Pure wallet clamp. Reduces a <see cref="ShiftPurchaseManifest"/> to the
/// maximum the player's wallet can afford this shift. Groups are visited in manifest order
/// (preferred store first), and lines within a group are funded in order — each group orders
/// fertilizer first (see <see cref="StorePurchaseGroup"/>), prioritizing it under a shortfall.
/// The result never exceeds the wallet and is monotonic in wallet gold. When the wallet covers
/// the whole manifest, every line is bought in full (the clamp never silently drops part of an
/// affordable order). Note: this clamp does not try to keep bought seeds and fertilizer in
/// lockstep — that isn't a correctness requirement, because the planting phase only plants tiles
/// it can both seed and fertilize, and any surplus stays safely in the chest for the next shift.
/// </summary>
public sealed class PurchaseAffordabilityCalculator
{
    public AffordablePurchasePlan ClampToWallet(ShiftPurchaseManifest manifest, int walletGold)
    {
        if (manifest is null || !manifest.HasPurchases)
            return AffordablePurchasePlan.Empty;

        var remaining = Math.Max(0, walletGold);
        var shortfall = false;
        var clampedGroups = new List<StorePurchaseGroup>();

        foreach (var group in manifest.Groups)
        {
            var clampedLines = new List<ManifestLine>();

            foreach (var line in group.Lines)
                ClampSingleLine(line, clampedLines, ref remaining, ref shortfall);

            if (clampedLines.Count > 0)
                clampedGroups.Add(new StorePurchaseGroup(group.Store, clampedLines));
        }

        return new AffordablePurchasePlan(clampedGroups, shortfall);
    }

    private static void ClampSingleLine(
        ManifestLine line,
        List<ManifestLine> clampedLines,
        ref int remaining,
        ref bool shortfall)
    {
        var affordableQty = line.UnitCost <= 0
            ? line.Quantity
            : Math.Min(line.Quantity, remaining / line.UnitCost);

        if (affordableQty < line.Quantity)
            shortfall = true;

        if (affordableQty <= 0)
            return;

        clampedLines.Add(line with { Quantity = affordableQty });
        remaining -= affordableQty * Math.Max(0, line.UnitCost);
    }
}
