using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Scheduling;

public sealed class OwnerAvailabilitySchedulingTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Dayswork2Review_R5_OfflinePreferenceControlsDayStartEligibility(
        bool runWhileOffline,
        bool ownerConnected,
        bool expected) =>
        Assert.Equal(expected, RecurringContractScheduler.CanRunForOwner(runWhileOffline, ownerConnected));
}
