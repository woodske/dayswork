namespace Dayswork.Tests.Shifts;

using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

public sealed class ShiftStateMachineStopReasonTests
{
    [Fact]
    public void BeginWrapUp_records_stop_reason_and_enters_depositing()
    {
        var stateMachine = new ShiftStateMachine();
        stateMachine.Transition(ShiftPhase.Working, new IntentMoveToTile(new TileCoord(1, 1)));

        stateMachine.BeginWrapUp(new IntentDepositInShippingBin(), ShiftStopReason.Exhausted);

        Assert.Equal(ShiftPhase.Depositing, stateMachine.Phase);
        Assert.Equal(ShiftStopReason.Exhausted, stateMachine.StopReason);
    }

    [Fact]
    public void RegisterStopReason_does_not_overwrite_existing_reason()
    {
        var stateMachine = new ShiftStateMachine();

        stateMachine.RegisterStopReason(ShiftStopReason.HardCap);
        stateMachine.RegisterStopReason(ShiftStopReason.Sleep);

        Assert.Equal(ShiftStopReason.HardCap, stateMachine.StopReason);
    }
}
