using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ShiftContextTests
{
    [Fact]
    public void ComputeRefund_WhenStoppedAtSleepTime_ChargesElapsedWholeHours()
    {
        var ctx = new ShiftContext(
            ContractId.New(),
            Array.Empty<Zone>(),
            new HashSet<TaskKind>(),
            new Dictionary<TaskKind, DestinationKey>(),
            depositAmount: 1_000,
            hourlyRate: 200,
            new ToolSnapshot(ToolLevel.Basic, ToolLevel.Basic, ToolLevel.Basic),
            Array.Empty<WorkItem>(),
            shiftStartTime: 600)
        {
            ShiftEndTime = 930,
        };

        Assert.Equal(400, ctx.ComputeRefund());
    }

    [Fact]
    public void ComputeRefund_WhenStoppedAtStart_RefundsFullDeposit()
    {
        var ctx = new ShiftContext(
            ContractId.New(),
            Array.Empty<Zone>(),
            new HashSet<TaskKind>(),
            new Dictionary<TaskKind, DestinationKey>(),
            depositAmount: 1_000,
            hourlyRate: 200,
            new ToolSnapshot(ToolLevel.Basic, ToolLevel.Basic, ToolLevel.Basic),
            Array.Empty<WorkItem>(),
            shiftStartTime: 600)
        {
            ShiftEndTime = 600,
        };

        Assert.Equal(1_000, ctx.ComputeRefund());
    }
}
