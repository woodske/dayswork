using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A destination-agnostic quantity of one item, used inside trips and overflow sets
// after consolidation (same item id + quality [+ flavor] are summed).
// FlavorId: see BufferedItem — opaque token so flavored items rebuild faithfully and don't merge
// with the plain variant of the same base id.
public sealed record ItemStack(string QualifiedItemId, int Quantity, int Quality = 0, string? FlavorId = null);

// One physical visit to one walkable destination, carrying every item bound for it.
// Destination is never AutomaticOutputDestination (automatic-overflow items are not walked to);
// Items is non-empty.
public sealed record DepositTrip(
    DestinationKey Destination,
    TileCoord Tile,
    IReadOnlyList<RoutedItemStack> Items);

// The complete routing result for a shift's buffer.
// Conservation invariant: items across Trips[*].Items ∪ AutomaticOverflow == input snapshot.
public sealed record DepositPlan(
    IReadOnlyList<DepositTrip> Trips,
    IReadOnlyList<RoutedItemStack> AutomaticOverflow);

// Why an item ended up in automatic overflow rather than a walked deposit.
public enum OverflowReason
{
    NoChestAssigned, // task had no assignment ⇒ AutomaticOutputDestination
    ChestFull,       // assigned chest could not hold all items
    ChestMissing,    // assigned chest was moved/destroyed
    NotDelivered,    // player slept/saved before the deposit run finished
    ChestBusy,       // assigned chest was being accessed by a farmer (mutex held) when the worker arrived
}

// An undeliverable item plus the reason; the union is delivered by the shift outcome dispatcher.
public sealed record OverflowItem(RoutedItemStack Stack, OverflowReason Reason);
