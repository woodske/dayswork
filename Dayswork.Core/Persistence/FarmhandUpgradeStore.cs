using Dayswork.Core.Upgrades;

namespace Dayswork.Core.Persistence;

/// <summary>
/// Farmhand upgrades, per owner. Upgrades used to be one global set, which is right for
/// single-player (one owner) and wrong twice over in co-op: a guest would inherit whatever the
/// host had bought, and — because a contract's upgrades come from its office's owner — one player's
/// purchase would silently speed up everybody's workers. Keyed by
/// <c>Farmer.UniqueMultiplayerID</c>, single-player behaviour is unchanged and co-op is fair
/// (2.0 plan D9).
/// <para>
/// This is host state: it lives in host save data, and only the host mutates it. A client learns
/// its own entry from the menu snapshot.
/// </para>
/// </summary>
public sealed class FarmhandUpgradeStore
{
    private readonly Dictionary<long, FarmhandUpgradeState> _byOwner = new();

    /// <summary>The owner's upgrades — <see cref="FarmhandUpgradeState.Empty"/> for an owner who
    /// has bought nothing, which is also the right answer for an id that no longer resolves.</summary>
    public FarmhandUpgradeState For(long ownerId) =>
        _byOwner.TryGetValue(ownerId, out var state) ? state : FarmhandUpgradeState.Empty;

    public bool IsPurchased(long ownerId, FarmhandUpgradeKind kind) => For(ownerId).IsPurchased(kind);

    public void Replace(long ownerId, FarmhandUpgradeState state) => _byOwner[ownerId] = state;

    public void Hydrate(IReadOnlyDictionary<long, FarmhandUpgradeState> byOwner)
    {
        _byOwner.Clear();
        foreach (var (ownerId, state) in byOwner)
            _byOwner[ownerId] = state;
    }

    /// <summary>Every owner's entry, for persistence. Owners with nothing purchased are not
    /// stored.</summary>
    public IReadOnlyDictionary<long, FarmhandUpgradeState> All =>
        _byOwner
            .Where(entry => entry.Value != FarmhandUpgradeState.Empty)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

    public void Clear() => _byOwner.Clear();
}
