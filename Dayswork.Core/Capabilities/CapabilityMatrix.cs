using Dayswork.Core.Domain;

namespace Dayswork.Core.Capabilities;

/// <summary>
/// Static lookup table encoding the spec's Tool-inheritance table as threshold comparisons.
/// Called internally by CapabilityEvaluator; never instantiated.
/// </summary>
public static class CapabilityMatrix
{
    /// <summary>
    /// Returns true if the given axe level can process the target object.
    /// FruitTree always returns false regardless of level (FR-SKIP-03).
    /// </summary>
    public static bool CanChop(ToolLevel axeLevel, AxeTarget target) => target switch
    {
        AxeTarget.FruitTree    => false,                          // FR-SKIP-03: unconditional hard rule
        AxeTarget.LargeLog     => axeLevel >= ToolLevel.Gold,     // Gold+ only
        AxeTarget.LargeStump   => axeLevel >= ToolLevel.Steel,    // Steel+ only
        AxeTarget.StandingTree => true,                           // any level
        AxeTarget.SmallStump   => true,                           // any level
        _                      => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    /// <summary>
    /// Returns true if the given pickaxe level can process the target object.
    /// </summary>
    public static bool CanBreak(ToolLevel pickLevel, PickTarget target) => target switch
    {
        PickTarget.Meteorite    => pickLevel >= ToolLevel.Gold,   // Gold+ only
        PickTarget.LargeBoulder => pickLevel >= ToolLevel.Steel,  // Steel+ only
        PickTarget.SmallRock    => true,                          // any level
        _                       => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };
}
