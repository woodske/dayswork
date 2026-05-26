namespace Dayswork.Tests.U24;

using Dayswork.Core.Shifts;
using Xunit;

public sealed class HitReactionPolicyTests
{
    [Fact]
    public void ShouldTriggerEmote_returns_true_for_a_fresh_swing_in_range()
    {
        Assert.True(HitReactionPolicy.ShouldTriggerEmote(
            isSwinging: true,
            wasSwinging: false,
            distanceTiles: 2f,
            hitRangeTiles: 2f));
    }

    [Fact]
    public void ShouldTriggerEmote_returns_false_when_the_player_is_already_mid_swing()
    {
        Assert.False(HitReactionPolicy.ShouldTriggerEmote(
            isSwinging: true,
            wasSwinging: true,
            distanceTiles: 1f,
            hitRangeTiles: 2f));
    }

    [Fact]
    public void ShouldTriggerEmote_returns_false_when_the_player_is_out_of_range()
    {
        Assert.False(HitReactionPolicy.ShouldTriggerEmote(
            isSwinging: true,
            wasSwinging: false,
            distanceTiles: 3f,
            hitRangeTiles: 2f));
    }
}
