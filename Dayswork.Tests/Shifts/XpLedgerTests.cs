using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class XpLedgerTests
{
    private const int Farming = 0;
    private const int Foraging = 2;
    private const int Mining = 3;
    private const int Luck = 5;

    [Fact]
    public void NewLedger_HasNothingPending()
    {
        var ledger = new XpLedger();

        Assert.Equal(0, ledger.TotalPending);
        Assert.Empty(ledger.Flush());
    }

    [Fact]
    public void Add_AccumulatesPerSkill()
    {
        var ledger = new XpLedger();

        ledger.Add(Farming, 5);
        ledger.Add(Farming, 7);
        ledger.Add(Mining, 3);

        Assert.Equal(12, ledger.Pending(Farming));
        Assert.Equal(3, ledger.Pending(Mining));
        Assert.Equal(0, ledger.Pending(Foraging));
        Assert.Equal(15, ledger.TotalPending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Add_IgnoresNonPositiveAmounts(int amount)
    {
        var ledger = new XpLedger();

        ledger.Add(Farming, amount);

        Assert.Equal(0, ledger.TotalPending);
    }

    // Vanilla gainExperience returns early for luck (skill 5), so banking it would be XP that can
    // never be delivered.
    [Theory]
    [InlineData(Luck)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Add_IgnoresSkillsOutsideTheGrantableRange(int skill)
    {
        var ledger = new XpLedger();

        ledger.Add(skill, 10);

        Assert.Equal(0, ledger.TotalPending);
        Assert.Equal(0, ledger.Pending(skill));
    }

    // The beat harvest hands over a whole experiencePoints-shaped array, luck slot included.
    [Fact]
    public void AddArray_TakesTheGrantableSkillsAndDropsTheRest()
    {
        var ledger = new XpLedger();

        ledger.Add(new[] { 4, 0, 14, 0, 2, 100 });

        Assert.Equal(4, ledger.Pending(0));
        Assert.Equal(14, ledger.Pending(2));
        Assert.Equal(2, ledger.Pending(4));
        Assert.Equal(20, ledger.TotalPending);
    }

    [Fact]
    public void AddArray_ShorterThanTheSkillCount_IsAccepted()
    {
        var ledger = new XpLedger();

        ledger.Add(new[] { 6, 1 });

        Assert.Equal(7, ledger.TotalPending);
    }

    [Fact]
    public void Flush_ReturnsOnlyNonZeroSkillsInSkillOrder()
    {
        var ledger = new XpLedger();
        ledger.Add(Mining, 3);
        ledger.Add(Farming, 5);

        var grants = ledger.Flush();

        Assert.Equal(
            new[] { new SkillXpGrant(Farming, 5), new SkillXpGrant(Mining, 3) },
            grants);
    }

    [Fact]
    public void Flush_ClearsWhatItHandedOver()
    {
        var ledger = new XpLedger();
        ledger.Add(Farming, 5);

        ledger.Flush();

        Assert.Equal(0, ledger.TotalPending);
        Assert.Empty(ledger.Flush());
    }

    // Grants are chunked at batch boundaries, so the ledger must keep counting after a flush.
    [Fact]
    public void Flush_ThenAdd_StartsANewBatchTotal()
    {
        var ledger = new XpLedger();
        ledger.Add(Farming, 5);
        ledger.Flush();

        ledger.Add(Farming, 8);

        Assert.Equal(new[] { new SkillXpGrant(Farming, 8) }, ledger.Flush());
    }
}
