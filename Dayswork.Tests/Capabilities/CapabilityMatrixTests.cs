using Dayswork.Core.Capabilities;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Capabilities;

public class CapabilityMatrixTests
{
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
        Assert.Equal(expected, CapabilityMatrix.CanChop(axeLevel, target));
    }

    // ── FruitTree always false regardless of axe level ───────────
    // 5 cases — explicit named test for the hard rule

    [Theory]
    [InlineData(ToolLevel.Basic)]
    [InlineData(ToolLevel.Copper)]
    [InlineData(ToolLevel.Steel)]
    [InlineData(ToolLevel.Gold)]
    [InlineData(ToolLevel.Iridium)]
    public void FruitTree_AlwaysReturnsFalse(ToolLevel axeLevel)
    {
        Assert.False(CapabilityMatrix.CanChop(axeLevel, AxeTarget.FruitTree));
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
        Assert.Equal(expected, CapabilityMatrix.CanBreak(pickLevel, target));
    }
}
