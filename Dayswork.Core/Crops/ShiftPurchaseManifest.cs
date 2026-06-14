namespace Dayswork.Core.Crops;

/// <summary>One priced purchase line in the shift manifest: how many of an item to buy and its
/// live unit cost. The unit cost is the planning price (read live once per shift); the headless
/// transaction re-reads the authoritative price at the counter.</summary>
public sealed record ManifestLine(string ItemId, int Quantity, int UnitCost, bool IsFertilizer)
{
    public int Quantity { get; init; } = Math.Max(0, Quantity);
    public int UnitCost { get; init; } = Math.Max(0, UnitCost);

    public int LineCost => Quantity * UnitCost;
}

/// <summary>
/// The whole-shift purchase intent, aggregated across every managed zone and grouped
/// by store in visit order (preferred store first, then the other). Sized up front from planned
/// plantable tiles and resolved against live store stock + prices.
/// </summary>
public sealed record ShiftPurchaseManifest
{
    public static ShiftPurchaseManifest Empty { get; } =
        new(Array.Empty<StorePurchaseGroup>(), Array.Empty<string>());

    /// <summary>Per-store purchase groups, ordered preferred-first.</summary>
    public IReadOnlyList<StorePurchaseGroup> Groups { get; }

    /// <summary>
    /// Items that were needed but no open store stocks (e.g. chest-supply-only crops). Informational
    /// only — these never trigger a trip or a purchase notice.
    /// </summary>
    public IReadOnlyList<string> ChestSupplyOnlyItems { get; }

    public ShiftPurchaseManifest(
        IReadOnlyList<StorePurchaseGroup>? groups,
        IReadOnlyList<string>? chestSupplyOnlyItems)
    {
        Groups = (groups ?? Array.Empty<StorePurchaseGroup>())
            .Where(group => group.Lines.Count > 0)
            .ToList()
            .AsReadOnly();
        ChestSupplyOnlyItems = (chestSupplyOnlyItems ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public bool HasPurchases => Groups.Count > 0;

    public int TotalCost => Groups.Sum(group => group.TotalCost);
}

/// <summary>One store's worth of priced purchase lines.</summary>
public sealed record StorePurchaseGroup
{
    public Store Store { get; }
    public IReadOnlyList<ManifestLine> Lines { get; }

    public StorePurchaseGroup(Store store, IReadOnlyList<ManifestLine>? lines)
    {
        Store = store;
        // Fertilizer is funded before its seed so a budget cut never leaves seed un-fertilizable.
        Lines = (lines ?? Array.Empty<ManifestLine>())
            .Where(line => line.Quantity > 0)
            .OrderByDescending(line => line.IsFertilizer)
            .ThenBy(line => line.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public int TotalCost => Lines.Sum(line => line.LineCost);
}
