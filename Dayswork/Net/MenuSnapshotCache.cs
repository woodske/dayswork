using Dayswork.Core.Net;
using Dayswork.Core.Upgrades;
using StardewModdingAPI.Utilities;

namespace Dayswork.Net;

/// <summary>
/// What a client knows about the parts of the host's world its own game does not sync: locations
/// outside the farm (expansion maps, the greenhouse) and the chests in them, plus its own farmhand
/// upgrades, which live in host save data.
///
/// The two affected pickers are <b>restricted, not synced</b> (2.0 plan D3): until the snapshot
/// arrives they show nothing and say so, and afterwards they offer exactly what the host listed.
/// The host re-validates every reference at commit time regardless, so a stale snapshot costs a
/// rejection, never a bad contract.
///
/// Upgrade state is held apart from the world snapshot (R8). The two arrive together but are not
/// the same fact: a purchase acknowledgment updates upgrades with no world snapshot in hand, and
/// "we have not been told yet" has to be distinguishable from "nothing is purchased" — otherwise
/// the Upgrades page reads a fresh connection as an empty purchase history and relocks Speed2.
/// The host's pricing tables are held apart for the same reason, and because a client that priced a
/// draft from its own config file would show the player one bargain and be charged another (R9).
///
/// Per screen, because in split-screen two players can have the hub open at once.
/// </summary>
internal static class MenuSnapshotCache
{
    private static readonly PerScreen<MenuSnapshotResponseMessage?> Snapshot = new();
    private static readonly PerScreen<FarmhandUpgradeState?> UpgradeState = new();
    private static readonly PerScreen<SnapshotPricingConfig?> Pricing = new();
    private static readonly PerScreen<string> PendingRequestId = new(() => "");

    /// <summary>The most recent world snapshot for this screen, or null while one is outstanding.</summary>
    public static MenuSnapshotResponseMessage? Current
    {
        get => Snapshot.Value;
        set => Snapshot.Value = value;
    }

    /// <summary>
    /// True when this screen is a REMOTE client that has asked for a snapshot and not got one — the
    /// state the pickers label "waiting for the host". A split-screen guest never waits: it shares
    /// the host's process and reads the world directly, so it asks for no snapshot at all.
    /// </summary>
    public static bool IsWaiting => Guards.Authority.IsRemoteClient && Snapshot.Value is null;

    public static IReadOnlyList<SnapshotLocation> ExpansionLocations =>
        Snapshot.Value?.ExpansionLocations ?? (IReadOnlyList<SnapshotLocation>)Array.Empty<SnapshotLocation>();

    public static IReadOnlyList<SnapshotChest> OffFarmChests =>
        Snapshot.Value?.OffFarmChests ?? (IReadOnlyList<SnapshotChest>)Array.Empty<SnapshotChest>();

    /// <summary>This client's own upgrades as the host last reported them, or null while that is
    /// still unknown. Null is not "nothing purchased" — the page says it is waiting rather than
    /// showing a purchase history it does not have.</summary>
    public static FarmhandUpgradeState? Upgrades => UpgradeState.Value;

    /// <summary>
    /// The host's pricing and energy tables, or null while unknown. Held apart from the world
    /// snapshot for the same reason upgrades are: a requote answer carries fresh pricing with no
    /// world snapshot in hand, and pricing is about the host rather than about one office, so it
    /// survives opening a different one (R9).
    /// </summary>
    public static SnapshotPricingConfig? PricingConfig => Pricing.Value;

    /// <summary>Records the id of the snapshot request now outstanding, so an answer to a request
    /// this screen has already replaced (a second office opened, a retry) is ignored.</summary>
    public static void BeginRequest(string requestId) => PendingRequestId.Value = requestId;

    /// <summary>
    /// Takes the host's answer, if it is the one this screen is waiting for. The upgrade half is
    /// merged rather than assigned: upgrades are only ever bought, never given back, so OR-ing them
    /// means a snapshot that was in flight while a purchase completed cannot relock what the player
    /// has just paid for.
    /// </summary>
    public static void ApplySnapshot(MenuSnapshotResponseMessage snapshot)
    {
        if (!string.Equals(snapshot.RequestId, PendingRequestId.Value, StringComparison.Ordinal))
            return;

        PendingRequestId.Value = "";
        Snapshot.Value = snapshot;
        ApplyPricing(snapshot.Pricing);
        Merge(new FarmhandUpgradeState(
            snapshot.SpeedPurchased,
            snapshot.EnergyPurchased,
            snapshot.Speed2Purchased));
    }

    /// <summary>Takes the host's pricing tables from whichever answer carried them — the menu
    /// snapshot, or a requote. Ignores a null, which is an answer that simply had nothing new to
    /// say about pricing.</summary>
    public static void ApplyPricing(SnapshotPricingConfig? pricing)
    {
        if (pricing is not null)
            Pricing.Value = pricing;
    }

    /// <summary>
    /// Folds the upgrade state an action response carries back in, so a client's upgrades page
    /// redraws from the purchase it just made without another round trip — and does so even when
    /// no world snapshot has arrived, which is the case when Upgrades was opened from Manage.
    /// No-op on the host, which reads the store.
    /// </summary>
    public static void ApplyUpgradeState(ContractActionResponseMessage response)
    {
        if (!response.Accepted)
            return;

        Merge(new FarmhandUpgradeState(
            response.SpeedPurchased,
            response.EnergyPurchased,
            response.Speed2Purchased));
    }

    public static void ClearCurrentScreen()
    {
        Snapshot.Value = null;
        UpgradeState.Value = null;
        Pricing.Value = null;
        PendingRequestId.Value = "";
    }

    /// <summary>Drops the world half only — the pickers must not show the previous office's
    /// locations and chests while a new request is outstanding. Upgrades belong to the player, not
    /// the office, so they survive.</summary>
    public static void ClearWorldSnapshot() => Snapshot.Value = null;

    private static void Merge(FarmhandUpgradeState incoming) =>
        UpgradeState.Value = FarmhandUpgradeState.Merge(UpgradeState.Value, incoming);
}
