using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class ShiftFleetLifecycleTests
{
    [Fact]
    public void Dayswork2Review_R1_GuestDayStartPreservesClaimsAndShoppingReservations()
    {
        var fleet = new ShiftFleet(() => throw new InvalidOperationException("No shift should start in this test."));
        fleet.BeginDay(isHostScreen: true);
        var hostDay = fleet.CurrentDay;
        var first = ContractId.New();
        var second = ContractId.New();
        var claim = WorkClaimKey.TileTask("Farm", new TileCoord(4, 7), TaskKind.WaterCrops);
        hostDay.Claims.TryClaim(claim, first);
        hostDay.ShoppingBudgetFor(walletId: 12).Reserve(first, 900);

        fleet.BeginDay(isHostScreen: false);

        Assert.Same(hostDay, fleet.CurrentDay);
        Assert.True(fleet.CurrentDay.Claims.IsClaimedByOther(claim, second));
        Assert.Equal(900, fleet.CurrentDay.ShoppingBudgetFor(walletId: 12).ReservedByOthers(second));
    }

    [Fact]
    public void Dayswork2Review_R1_HostDayStartCreatesFreshSharedDayState()
    {
        var fleet = new ShiftFleet(() => throw new InvalidOperationException("No shift should start in this test."));
        var previous = fleet.CurrentDay;

        fleet.BeginDay(isHostScreen: true);

        Assert.NotSame(previous, fleet.CurrentDay);
    }
}
