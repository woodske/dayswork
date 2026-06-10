namespace Dayswork.Core.Crops;

/// <summary>
/// Pure wallet clamp. Reduces a <see cref="ShiftPurchaseManifest"/> to the
/// maximum the player's wallet can afford this shift. Groups are visited in manifest order
/// (preferred store first). Within each group, paired seed/fertilizer lines are clamped to the
/// same quantity before unrelated lines are funded, so a shortfall doesn't buy fertilizer without
/// its corresponding seed or vice versa. The result never exceeds the wallet and is monotonic in
/// wallet gold.
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

            var remainingLines = group.Lines.ToList();
            while (remainingLines.Count > 0)
            {
                var line = remainingLines[0];
                remainingLines.RemoveAt(0);

                if (line.IsFertilizer && TryPopFirstSeed(remainingLines, out var seedLine))
                    ClampPairedLines(line, seedLine, clampedLines, ref remaining, ref shortfall);
                else
                    ClampSingleLine(line, clampedLines, ref remaining, ref shortfall);
            }

            if (clampedLines.Count > 0)
                clampedGroups.Add(new StorePurchaseGroup(group.Store, clampedLines));
        }

        return new AffordablePurchasePlan(clampedGroups, shortfall);
    }

    private static void ClampPairedLines(
        ManifestLine fertilizerLine,
        ManifestLine seedLine,
        List<ManifestLine> clampedLines,
        ref int remaining,
        ref bool shortfall)
    {
        var requestedPairs = Math.Min(fertilizerLine.Quantity, seedLine.Quantity);
        if (requestedPairs <= 0)
            return;

        var unitCost = Math.Max(0, fertilizerLine.UnitCost) + Math.Max(0, seedLine.UnitCost);
        var affordablePairs = unitCost <= 0
            ? requestedPairs
            : Math.Min(requestedPairs, remaining / unitCost);

        if (affordablePairs < fertilizerLine.Quantity || affordablePairs < seedLine.Quantity)
            shortfall = true;

        if (affordablePairs <= 0)
            return;

        clampedLines.Add(fertilizerLine with { Quantity = affordablePairs });
        clampedLines.Add(seedLine with { Quantity = affordablePairs });
        remaining -= affordablePairs * unitCost;
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

    private static bool TryPopFirstSeed(List<ManifestLine> lines, out ManifestLine seedLine)
    {
        var index = lines.FindIndex(line => !line.IsFertilizer);
        if (index < 0)
        {
            seedLine = null!;
            return false;
        }

        seedLine = lines[index];
        lines.RemoveAt(index);
        return true;
    }
}
