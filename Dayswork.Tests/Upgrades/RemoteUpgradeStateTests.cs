using Dayswork.Core.Upgrades;
using Xunit;

namespace Dayswork.Tests.Upgrades;

/// <summary>
/// R8: a remote client's view of its own purchased upgrades. Upgrades live in host save data, so
/// the client only ever knows what the host has told it — and the host's two answers (a menu
/// snapshot and a purchase acknowledgment) can arrive in either order.
/// </summary>
public sealed class RemoteUpgradeStateTests
{
    [Fact]
    public void Dayswork2Review_R8_FirstAnswerBecomesTheKnownState()
    {
        var state = FarmhandUpgradeState.Merge(null, new FarmhandUpgradeState(true, false, true));

        Assert.True(state.IsPurchased(FarmhandUpgradeKind.Speed));
        Assert.True(state.IsPurchased(FarmhandUpgradeKind.Speed2));
        Assert.False(state.IsPurchased(FarmhandUpgradeKind.Energy));
    }

    [Fact]
    public void Dayswork2Review_R8_LateSnapshotCannotRelockAnAcknowledgedPurchase()
    {
        // The purchase acknowledgment lands first; the snapshot was already in flight and still
        // describes the world before it.
        var afterPurchase = FarmhandUpgradeState.Merge(null, new FarmhandUpgradeState(true, false, false));
        var afterLateSnapshot = FarmhandUpgradeState.Merge(afterPurchase, FarmhandUpgradeState.Empty);

        Assert.True(afterLateSnapshot.IsPurchased(FarmhandUpgradeKind.Speed));
    }

    [Fact]
    public void Dayswork2Review_R8_SnapshotOwnershipSurvivesAnUnrelatedAcknowledgment()
    {
        var afterSnapshot = FarmhandUpgradeState.Merge(null, new FarmhandUpgradeState(true, false, true));
        var afterEnergyPurchase = FarmhandUpgradeState.Merge(
            afterSnapshot,
            new FarmhandUpgradeState(true, true, true));

        Assert.True(afterEnergyPurchase.IsPurchased(FarmhandUpgradeKind.Speed));
        Assert.True(afterEnergyPurchase.IsPurchased(FarmhandUpgradeKind.Speed2));
        Assert.True(afterEnergyPurchase.IsPurchased(FarmhandUpgradeKind.Energy));
    }

    [Fact]
    public void Dayswork2Review_R8_UnknownIsNotTheSameAsNothingPurchased()
    {
        // Null is the "not told yet" the page shows as waiting; Empty is a real, known answer.
        FarmhandUpgradeState? unknown = null;

        Assert.NotEqual(FarmhandUpgradeState.Empty, unknown);
        Assert.Equal(FarmhandUpgradeState.Empty, FarmhandUpgradeState.Merge(unknown, FarmhandUpgradeState.Empty));
    }
}
