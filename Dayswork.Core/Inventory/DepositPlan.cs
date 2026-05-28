using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A destination-agnostic quantity of one item, used inside trips and overflow sets
// after consolidation (identical item ids are summed).
public sealed record ItemStack(string QualifiedItemId, int Quantity);

// One physical visit to one walkable destination, carrying every item bound for it.
// Destination is never MailDestination (mail items are not walked to); Items is non-empty.
public sealed record DepositTrip(
    DestinationKey Destination,
    TileCoord Tile,
    IReadOnlyList<RoutedItemStack> Items);

// The complete routing result for a shift's buffer (Pattern M).
// Conservation invariant: items across Trips[*].Items ∪ PreMailedOverflow == input snapshot.
public sealed record DepositPlan(
    IReadOnlyList<DepositTrip> Trips,
    IReadOnlyList<RoutedItemStack> PreMailedOverflow);

// Why an item ended up mailed rather than deposited. Drives the overflow letter body (FD-Q6=A).
public enum OverflowReason
{
    NoChestAssigned, // task had no assignment ⇒ MailDestination (FD-Q2=A / FR-OUT-04)
    ChestFull,       // assigned chest could not hold all items (FR-OUT-02)
    ChestMissing,    // assigned chest was moved/destroyed (FR-OUT-03)
    NotDelivered,    // player slept/saved before the deposit run finished (FD-Q5=A)
    ChestBusy,       // assigned chest was being accessed by a farmer (mutex held) when the worker arrived
}

// An undeliverable item plus the reason; the union becomes the single overflow letter (Pattern O).
public sealed record OverflowItem(RoutedItemStack Stack, OverflowReason Reason);
