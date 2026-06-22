using Dayswork.Core.Upgrades;

namespace Dayswork.Core.Persistence;

public sealed class FarmhandUpgradeStore
{
    public FarmhandUpgradeState State { get; private set; } = FarmhandUpgradeState.Empty;

    public void Hydrate(FarmhandUpgradeState state)
    {
        State = state;
    }

    public bool IsPurchased(FarmhandUpgradeKind kind) => State.IsPurchased(kind);

    public void Replace(FarmhandUpgradeState state)
    {
        State = state;
    }
}
