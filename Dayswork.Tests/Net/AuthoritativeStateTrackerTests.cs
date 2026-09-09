using Dayswork.Core.Net;
using Xunit;

namespace Dayswork.Tests.Net;

/// <summary>
/// R7: the ordering rule a client applies to the host's authoritative office state. Two answers
/// about the same office can arrive out of order — a rejection overtaking an acceptance, or a
/// request-id replay of an older answer — and the later one must win regardless.
/// </summary>
public sealed class AuthoritativeStateTrackerTests
{
    private const string Office = "0f8a4c1d2e3b4f5a6c7d8e9f0a1b2c3d";

    private static AuthoritativeContractState State(long sequence, string officeId = Office) =>
        new() { OfficeId = officeId, Sequence = sequence };

    [Fact]
    public void Dayswork2Review_R7_FirstStateForAnOfficeIsApplied() =>
        Assert.True(new AuthoritativeStateTracker().TryAccept(State(1)));

    [Fact]
    public void Dayswork2Review_R7_OlderStateCannotOverwriteNewer()
    {
        var sut = new AuthoritativeStateTracker();

        Assert.True(sut.TryAccept(State(5)));
        Assert.False(sut.TryAccept(State(4)));
        Assert.False(sut.TryAccept(State(5)));
        Assert.True(sut.TryAccept(State(6)));
    }

    [Fact]
    public void Dayswork2Review_R7_OfficesAreOrderedIndependently()
    {
        var sut = new AuthoritativeStateTracker();
        var other = "ffffffffffffffffffffffffffffffff";

        Assert.True(sut.TryAccept(State(9)));
        Assert.True(sut.TryAccept(State(2, other)));
    }

    [Fact]
    public void Dayswork2Review_R7_UnstampedStateIsNeverApplied()
    {
        var sut = new AuthoritativeStateTracker();

        Assert.False(sut.TryAccept(null));
        Assert.False(sut.TryAccept(State(0)));
        Assert.False(sut.TryAccept(State(3, officeId: "")));
    }

    [Fact]
    public void Dayswork2Review_R7_ClearLetsANewSessionStartFromOneAgain()
    {
        var sut = new AuthoritativeStateTracker();
        sut.TryAccept(State(12));

        sut.Clear();

        Assert.True(sut.TryAccept(State(1)));
    }
}
