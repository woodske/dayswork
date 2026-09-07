using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Integration;

// Dispatches shift-end overflow delivery plus same-day HUD notices for contract lifecycle events.
internal interface IShiftOutcomeDispatcher
{
    // Delivers every overflow item to the shift's OWN office chest, with a fallback to the
    // SPONSOR's shipping bin. Sends nothing when there are no items. office is the contract's
    // office building; a null office (demolished mid-shift) still routes everything to the bin, so
    // nothing is lost. flavorTemplates clones captured flavored/colored items (roe, wine…) back
    // faithfully — keyed by RoutedItemStack.FlavorId; pass an empty map for none.
    void DispatchOverflowDelivery(
        StardewValley.Buildings.Building? office,
        long ownerId,
        IReadOnlyList<ItemStack> items,
        IReadOnlyList<OverflowCategory> categories,
        IReadOnlyDictionary<string, StardewValley.Object> flavorTemplates);

    // Same-day HUD notice that the recurring contract's fixed daily price was unaffordable.
    void ShowCannotAffordNotice(Contract contract, int dailyPrice, int shortfall);

    // Same-day HUD notice that a recurring contract must be reviewed before it can run again.
    void ShowNeedsAttentionNotice(Contract contract);

    // Same-day festival HUD notice; one-time contract prices are refunded to the sponsor's wallet.
    void ShowFestivalNotice(Contract contract, int refundGold);

    // A contract could not be kept: its office is gone, or (on a 1.x save) there is no way to tell
    // which office it belonged to. The contract is dropped and its price is forfeited.
    void ShowContractLostNotice(long ownerId);
}
