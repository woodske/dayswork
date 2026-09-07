using Netcode;
using StardewValley;
using StardewValley.Inventories;

namespace Dayswork.Integration;

/// <summary>
/// The contract's sponsor: the player who pays for the shift, whose tools the worker inherits,
/// whose shipping bin receives output, and who earns the experience the worker generates.
/// <para>
/// This is the ONLY seam through which owner identity enters the shift engine. The worker's fake
/// action farmer deliberately keeps the host's <c>UniqueMultiplayerID</c> forever — vanilla gates
/// machine collection and tree XP on <c>Farmer.IsLocalPlayer</c>, which is identity-based, so
/// building it from a remote owner would break Manage Machines outright (see
/// <c>docs/game-data/multiplayer-and-ownership.md</c> and the 2.0 plan's correction 1).
/// </para>
/// <para>
/// In single-player the owner is always the local player, so every accessor here resolves to
/// today's behaviour.
/// </para>
/// </summary>
internal static class Sponsor
{
    /// <summary>The owner, online or offline, or null when the id is unknown (a deleted farmhand
    /// slot). Never <c>Game1.getFarmer</c>, which silently falls back to the host.</summary>
    public static Farmer? Resolve(long ownerId) => Game1.GetPlayer(ownerId);

    /// <summary>The owner, falling back to the host when the id no longer resolves — used where a
    /// missing owner must not take the whole operation down (wallet, shipping).</summary>
    public static Farmer ResolveOrHost(long ownerId) => Game1.GetPlayer(ownerId) ?? Game1.MasterPlayer;

    /// <summary>Whether the owner is currently connected. XP is granted only to a connected owner
    /// (an offline farmer's message queue is never flushed, so the grant would vanish).</summary>
    public static bool IsConnected(long ownerId) => Game1.GetPlayer(ownerId, onlyOnline: true) is not null;

    /// <summary>Whether the owner is the player at this screen — the test for whether a notice can
    /// be shown as a HUD message rather than logged.</summary>
    public static bool IsLocal(long ownerId) => Game1.player?.UniqueMultiplayerID == ownerId;

    /// <summary>The owner's wallet. One shared <see cref="NetIntDelta"/> under single-player and
    /// co-op's shared-money setting; the owner's own entry under separate wallets. Reading and
    /// writing through this covers both modes and survives a mid-save wallet switch.</summary>
    public static NetIntDelta Wallet(long ownerId) => Game1.player.team.GetMoney(ResolveOrHost(ownerId));

    public static int Money(long ownerId) => Wallet(ownerId).Value;

    /// <summary>
    /// Identity of the wallet this owner spends from — what a per-wallet ledger keys on. Under
    /// separate wallets that is the owner; under shared money (the co-op default, and the only
    /// mode single-player has) every owner shares one, so they must share one ledger too.
    /// </summary>
    public static long WalletId(long ownerId) =>
        Game1.player.team.useSeparateWallets.Value ? ownerId : SharedWalletId;

    /// <summary>Stand-in id for the shared wallet. No farmer has multiplayer id 0.</summary>
    private const long SharedWalletId = 0L;

    public static void Charge(long ownerId, int amount) => Wallet(ownerId).Value -= amount;

    public static void Credit(long ownerId, int amount) => Wallet(ownerId).Value += amount;

    /// <summary>The owner's shipping bin — their personal bin under separate wallets, the shared
    /// one otherwise.</summary>
    public static IInventory? ShippingBin(Farm? farm, long ownerId) =>
        farm?.getShippingBin(ResolveOrHost(ownerId));

    /// <summary>Ships one item to the owner's bin. <paramref name="animate"/> uses the vanilla
    /// lid animation + delayed "Ship" sound; the silent path is for off-screen settlement.</summary>
    public static void ShipItem(Farm farm, Item item, long ownerId, bool animate)
    {
        var owner = ResolveOrHost(ownerId);
        if (animate)
            farm.shipItem(item, owner);
        else
            farm.getShippingBin(owner).Add(item);
    }

    /// <summary>
    /// Grants experience the worker earned to its sponsor. A connected owner gets it through
    /// vanilla <c>gainExperience</c> — applied locally for the host, delivered by the game's own
    /// type-17 message (with the normal level-up UI) for an online guest. An owner who is not
    /// connected earns nothing: their message queue is never flushed, and nothing is banked.
    /// </summary>
    public static void GrantExperience(long ownerId, int skill, int amount)
    {
        if (amount <= 0 || skill < 0 || skill > 4)
            return;

        Game1.GetPlayer(ownerId, onlyOnline: true)?.gainExperience(skill, amount);
    }
}
