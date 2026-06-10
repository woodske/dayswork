namespace Dayswork.Core.Capabilities;

/// <summary>
/// Normalized classification of objects the axe can target.
/// The Mod layer maps Stardew Valley ResourceClump and Tree types to these values
/// before calling CapabilityEvaluator.
/// </summary>
public enum AxeTarget
{
    /// <summary>Normal felled tree (any species except fruit trees).</summary>
    StandingTree,

    /// <summary>Fruit tree — always skipped regardless of axe level (hard rule).</summary>
    FruitTree,

    /// <summary>Small stump left after the player fells a tree. Choppable at any axe level.</summary>
    SmallStump,

    /// <summary>Large hardwood stump. Requires Steel axe or better.</summary>
    LargeStump,

    /// <summary>Large fallen log. Requires Gold axe or better.</summary>
    LargeLog,
}
