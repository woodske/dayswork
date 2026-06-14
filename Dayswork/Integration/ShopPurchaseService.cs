namespace Dayswork.Integration;

using Dayswork.Core.Crops;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

/// <summary>
/// Gold/item transaction boundary. Purchases are exchange-atomic per line: build the
/// item stack, deposit as much as the input chest accepts, then deduct gold only for the accepted
/// quantity. Any bind/create failure returns <see cref="PurchaseResult.BindFailure"/> without
/// mutating gold.
/// </summary>
internal sealed class ShopPurchaseService
{
    private readonly IMonitor? _monitor;

    public ShopPurchaseService(IMonitor? monitor = null) => _monitor = monitor;

    public PurchaseResult BuyToInputChest(StorePurchaseGroup group, Chest inputChest, Farmer farmer)
    {
        var outcomes = new List<PurchaseLineOutcome>();

        foreach (var line in group.Lines)
        {
            if (line.Quantity <= 0)
                continue;

            var unitCost = ResolveUnitCost(group.Store, line);
            if (unitCost <= 0)
            {
                _monitor?.Log($"Dayswork shopping: could not resolve live price for '{line.ItemId}' at {group.Store}.", DevLog.WarnLevel);
                return PurchaseResult.BindFailure;
            }

            var maxAffordable = Math.Min(line.Quantity, farmer.Money / unitCost);
            if (maxAffordable <= 0)
            {
                outcomes.Add(BuildOutcome(group.Store, line, 0, unitCost, PurchaseOutcomeKind.Insufficient));
                continue;
            }

            var item = TryCreateItem(line.ItemId, maxAffordable);
            if (item is null)
            {
                _monitor?.Log($"Dayswork shopping: could not create item '{line.ItemId}'.", DevLog.WarnLevel);
                return PurchaseResult.BindFailure;
            }

            var leftover = inputChest.addItem(item);
            var accepted = maxAffordable - Math.Max(0, leftover?.Stack ?? 0);
            if (accepted > 0)
                farmer.Money -= accepted * unitCost;

            var kind = accepted switch
            {
                0 => PurchaseOutcomeKind.Insufficient,
                var bought when bought < line.Quantity => PurchaseOutcomeKind.Partial,
                _ => PurchaseOutcomeKind.Full,
            };
            outcomes.Add(BuildOutcome(group.Store, line, accepted, unitCost, kind));
        }

        return new PurchaseResult(outcomes, bindFailed: false);
    }

    public PurchaseResult BuyToCarriedItems(StorePurchaseGroup group, Farmer farmer, IList<Item> carriedItems)
    {
        var outcomes = new List<PurchaseLineOutcome>();

        foreach (var line in group.Lines)
        {
            if (line.Quantity <= 0)
                continue;

            var outcome = BuyLineToCarriedItems(group.Store, line, farmer, carriedItems);
            if (outcome is null)
                return PurchaseResult.BindFailure;
            outcomes.Add(outcome);
        }

        return new PurchaseResult(outcomes, bindFailed: false);
    }

    /// <summary>Buys a single line item. Returns null on bind/create failure (caller should abort).</summary>
    public PurchaseLineOutcome? BuyLineToCarriedItems(Store store, ManifestLine line, Farmer farmer, IList<Item> carriedItems)
    {
        var unitCost = ResolveUnitCost(store, line);
        if (unitCost <= 0)
        {
            _monitor?.Log($"Dayswork shopping: could not resolve live price for '{line.ItemId}' at {store}.", DevLog.WarnLevel);
            return null;
        }

        var maxAffordable = Math.Min(line.Quantity, farmer.Money / unitCost);
        if (maxAffordable <= 0)
            return BuildOutcome(store, line, 0, unitCost, PurchaseOutcomeKind.Insufficient);

        var item = TryCreateItem(line.ItemId, maxAffordable);
        if (item is null)
        {
            _monitor?.Log($"Dayswork shopping: could not create item '{line.ItemId}'.", DevLog.WarnLevel);
            return null;
        }

        carriedItems.Add(item);
        farmer.Money -= maxAffordable * unitCost;
        var kind = maxAffordable < line.Quantity
            ? PurchaseOutcomeKind.Partial
            : PurchaseOutcomeKind.Full;
        return BuildOutcome(store, line, maxAffordable, unitCost, kind);
    }

    private static PurchaseLineOutcome BuildOutcome(
        Store store,
        ManifestLine line,
        int boughtQty,
        int unitCost,
        PurchaseOutcomeKind kind) =>
        new(
            store,
            line.ItemId,
            ResolveDisplayName(line.ItemId),
            line.Quantity,
            boughtQty,
            unitCost,
            kind);

    private static int ResolveUnitCost(Store store, ManifestLine line) =>
        ShopStockReader.TryGetLiveUnitPrice(store, line.ItemId, out var liveUnitCost)
            ? liveUnitCost
            : Math.Max(0, line.UnitCost);

    private static Item? TryCreateItem(string itemId, int quantity)
    {
        try
        {
            var qualified = ItemRegistry.QualifyItemId(itemId) ?? $"(O){itemId}";
            var item = ItemRegistry.Create(qualified, quantity);
            return item is null || IsErrorItem(item) ? null : item;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveDisplayName(string itemId)
    {
        try
        {
            var qualified = ItemRegistry.QualifyItemId(itemId) ?? $"(O){itemId}";
            var data = ItemRegistry.GetDataOrErrorItem(qualified);
            return string.IsNullOrWhiteSpace(data.DisplayName) ? itemId : data.DisplayName;
        }
        catch
        {
            return itemId;
        }
    }

    private static bool IsErrorItem(Item item) =>
        string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);
}
