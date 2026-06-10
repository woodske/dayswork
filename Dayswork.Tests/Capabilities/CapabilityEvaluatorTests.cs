using Dayswork.Core.Capabilities;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Capabilities;

public class CapabilityEvaluatorTests
{
    private readonly CapabilityEvaluator _sut = new CapabilityEvaluator();

    // ── CanChop table ──────────────────────────────
    // 20 cases: 5 AxeLevel × 4 non-FruitTree AxeTarget values

    [Theory]
    [InlineData(ToolLevel.Basic,   AxeTarget.StandingTree, true)]
    [InlineData(ToolLevel.Basic,   AxeTarget.SmallStump,   true)]
    [InlineData(ToolLevel.Basic,   AxeTarget.LargeStump,   false)]
    [InlineData(ToolLevel.Basic,   AxeTarget.LargeLog,     false)]
    [InlineData(ToolLevel.Copper,  AxeTarget.StandingTree, true)]
    [InlineData(ToolLevel.Copper,  AxeTarget.SmallStump,   true)]
    [InlineData(ToolLevel.Copper,  AxeTarget.LargeStump,   false)]
    [InlineData(ToolLevel.Copper,  AxeTarget.LargeLog,     false)]
    [InlineData(ToolLevel.Steel,   AxeTarget.StandingTree, true)]
    [InlineData(ToolLevel.Steel,   AxeTarget.SmallStump,   true)]
    [InlineData(ToolLevel.Steel,   AxeTarget.LargeStump,   true)]
    [InlineData(ToolLevel.Steel,   AxeTarget.LargeLog,     false)]
    [InlineData(ToolLevel.Gold,    AxeTarget.StandingTree, true)]
    [InlineData(ToolLevel.Gold,    AxeTarget.SmallStump,   true)]
    [InlineData(ToolLevel.Gold,    AxeTarget.LargeStump,   true)]
    [InlineData(ToolLevel.Gold,    AxeTarget.LargeLog,     true)]
    [InlineData(ToolLevel.Iridium, AxeTarget.StandingTree, true)]
    [InlineData(ToolLevel.Iridium, AxeTarget.SmallStump,   true)]
    [InlineData(ToolLevel.Iridium, AxeTarget.LargeStump,   true)]
    [InlineData(ToolLevel.Iridium, AxeTarget.LargeLog,     true)]
    public void CanChop_ReturnsExpectedResult(ToolLevel axeLevel, AxeTarget target, bool expected)
    {
        var snap = new ToolSnapshot(axeLevel, ToolLevel.Basic, ToolLevel.Basic);
        Assert.Equal(expected, _sut.CanChop(snap, target));
    }

    // ── FruitTree always false regardless of axe level ───────────
    // 5 cases — explicit named test for the hard rule

    [Theory]
    [InlineData(ToolLevel.Basic)]
    [InlineData(ToolLevel.Copper)]
    [InlineData(ToolLevel.Steel)]
    [InlineData(ToolLevel.Gold)]
    [InlineData(ToolLevel.Iridium)]
    public void FruitTree_AlwaysReturnsFalse_FR_SKIP_03(ToolLevel axeLevel)
    {
        var snap = new ToolSnapshot(axeLevel, ToolLevel.Basic, ToolLevel.Basic);
        Assert.False(_sut.CanChop(snap, AxeTarget.FruitTree));
    }

    [Fact]
    public void CanChop_DoesNotDependOn_NonAxe_ToolLevels()
    {
        var basicSupport = new ToolSnapshot(ToolLevel.Steel, ToolLevel.Basic, ToolLevel.Basic);
        var upgradedSupport = new ToolSnapshot(ToolLevel.Steel, ToolLevel.Iridium, ToolLevel.Iridium);

        Assert.Equal(
            _sut.CanChop(basicSupport, AxeTarget.LargeStump),
            _sut.CanChop(upgradedSupport, AxeTarget.LargeStump));
    }

    // ── CanBreak table ─────────────────────────────
    // 15 cases: 5 PickaxeLevel × 3 PickTarget values

    [Theory]
    [InlineData(ToolLevel.Basic,   PickTarget.SmallRock,    true)]
    [InlineData(ToolLevel.Basic,   PickTarget.LargeBoulder, false)]
    [InlineData(ToolLevel.Basic,   PickTarget.Meteorite,    false)]
    [InlineData(ToolLevel.Copper,  PickTarget.SmallRock,    true)]
    [InlineData(ToolLevel.Copper,  PickTarget.LargeBoulder, false)]
    [InlineData(ToolLevel.Copper,  PickTarget.Meteorite,    false)]
    [InlineData(ToolLevel.Steel,   PickTarget.SmallRock,    true)]
    [InlineData(ToolLevel.Steel,   PickTarget.LargeBoulder, true)]
    [InlineData(ToolLevel.Steel,   PickTarget.Meteorite,    false)]
    [InlineData(ToolLevel.Gold,    PickTarget.SmallRock,    true)]
    [InlineData(ToolLevel.Gold,    PickTarget.LargeBoulder, true)]
    [InlineData(ToolLevel.Gold,    PickTarget.Meteorite,    true)]
    [InlineData(ToolLevel.Iridium, PickTarget.SmallRock,    true)]
    [InlineData(ToolLevel.Iridium, PickTarget.LargeBoulder, true)]
    [InlineData(ToolLevel.Iridium, PickTarget.Meteorite,    true)]
    public void CanBreak_ReturnsExpectedResult(ToolLevel pickLevel, PickTarget target, bool expected)
    {
        var snap = new ToolSnapshot(ToolLevel.Basic, pickLevel, ToolLevel.Basic);
        Assert.Equal(expected, _sut.CanBreak(snap, target));
    }

    [Fact]
    public void CanBreak_DoesNotDependOn_NonPickaxe_ToolLevels()
    {
        var basicSupport = new ToolSnapshot(ToolLevel.Basic, ToolLevel.Steel, ToolLevel.Basic);
        var upgradedSupport = new ToolSnapshot(ToolLevel.Iridium, ToolLevel.Steel, ToolLevel.Iridium);

        Assert.Equal(
            _sut.CanBreak(basicSupport, PickTarget.LargeBoulder),
            _sut.CanBreak(upgradedSupport, PickTarget.LargeBoulder));
    }
}
