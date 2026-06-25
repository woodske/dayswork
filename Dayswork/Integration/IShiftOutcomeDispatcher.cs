using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Integration;

// Dispatches shift-end overflow delivery plus same-day HUD notices for contract lifecycle events.
internal interface IShiftOutcomeDispatcher
{
    // Delivers every overflow item to the farmhand office chest, with a shipping-bin fallback.
    // Sends nothing when there are no items. flavorTemplates clones captured flavored/colored items
    // (roe, wine…) back faithfully — keyed by RoutedItemStack.FlavorId; pass an empty map for none.
    void DispatchOverflowDelivery(
        IReadOnlyList<ItemStack> items,
        IReadOnlyList<OverflowCategory> categories,
        IReadOnlyDictionary<string, StardewValley.Object> flavorTemplates);

    // Same-day HUD notice that the recurring contract's fixed daily price was unaffordable.
    void ShowCannotAffordNotice(Contract contract, int dailyPrice, int shortfall);

    // Same-day HUD notice that a recurring contract must be reviewed before it can run again.
    void ShowNeedsAttentionNotice(Contract contract);

    // Same-day festival HUD notice; one-time contract prices are refunded directly to player gold.
    void ShowFestivalNotice(Contract contract, int refundGold);
}
