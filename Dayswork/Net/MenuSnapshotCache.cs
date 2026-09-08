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
/// Per screen, because in split-screen two players can have the hub open at once.
/// </summary>
internal static class MenuSnapshotCache
{
    private static readonly PerScreen<MenuSnapshotResponseMessage?> Snapshot = new();

    /// <summary>The most recent snapshot for this screen, or null while one is outstanding.</summary>
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

    /// <summary>This client's own upgrades, as the host reported them. Empty until the snapshot
    /// arrives, which is the safe default: the upgrades page shows nothing purchased and a
    /// purchase attempt is answered by the host anyway.</summary>
    public static FarmhandUpgradeState Upgrades =>
        Snapshot.Value is { } snapshot
            ? new FarmhandUpgradeState(snapshot.SpeedPurchased, snapshot.EnergyPurchased, snapshot.Speed2Purchased)
            : FarmhandUpgradeState.Empty;

    /// <summary>
    /// Folds the upgrade state an action response carries back into the cached snapshot, so a
    /// client's upgrades page redraws from the purchase it just made without another round trip.
    /// No-op on the host, which reads the store.
    /// </summary>
    public static void ApplyUpgradeState(ContractActionResponseMessage response)
    {
        if (Snapshot.Value is not { } snapshot || !response.Accepted)
            return;

        snapshot.SpeedPurchased = response.SpeedPurchased;
        snapshot.Speed2Purchased = response.Speed2Purchased;
        snapshot.EnergyPurchased = response.EnergyPurchased;
    }

    public static void Clear() => Snapshot.ResetAllScreens();
}
