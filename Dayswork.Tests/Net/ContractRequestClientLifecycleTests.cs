using Dayswork.Net;
using Xunit;

namespace Dayswork.Tests.Net;

public sealed class ContractRequestClientLifecycleTests
{
    [Fact]
    public void Dayswork2Review_R1_ResettingGuestScreenPreservesOtherScreensPendingCallbacks()
    {
        var sut = new ContractRequestClient(null!, null!, null!, "2.0.0", new DaysworkSuspension(), null!);
        sut.Track(0, new ContractRequestClient.Pending("host", DateTime.UtcNow, _ => { }, null));
        sut.Track(1, new ContractRequestClient.Pending("guest", DateTime.UtcNow, null, _ => { }));

        sut.ResetScreen(1);

        Assert.Equal(1, sut.PendingCount(0));
        Assert.Equal(0, sut.PendingCount(1));
    }
}
