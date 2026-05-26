namespace Dayswork.Tests.U24;

using Dayswork.Core.Guards;
using Xunit;

public sealed class BulletinBoardInteractionPolicyTests
{
    [Fact]
    public void Evaluate_blocks_multiplayer_before_any_click_action()
    {
        var actual = BulletinBoardInteractionPolicy.Evaluate(
            isMultiplayer: true,
            hireClicked: true,
            manageClicked: true);

        Assert.Equal(BulletinBoardInteractionAction.BlockedByMultiplayer, actual);
    }

    [Fact]
    public void Evaluate_prefers_hire_when_hire_button_is_clicked()
    {
        var actual = BulletinBoardInteractionPolicy.Evaluate(
            isMultiplayer: false,
            hireClicked: true,
            manageClicked: true);

        Assert.Equal(BulletinBoardInteractionAction.OpenHire, actual);
    }

    [Fact]
    public void Evaluate_returns_none_when_no_dayswork_button_was_clicked()
    {
        var actual = BulletinBoardInteractionPolicy.Evaluate(
            isMultiplayer: false,
            hireClicked: false,
            manageClicked: false);

        Assert.Equal(BulletinBoardInteractionAction.None, actual);
    }
}
