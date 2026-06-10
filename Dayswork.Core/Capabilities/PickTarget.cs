namespace Dayswork.Core.Capabilities;

/// <summary>
/// Normalized classification of objects the pickaxe can target.
/// The Mod layer maps Stardew Valley ResourceClump and Object types to these values
/// before calling CapabilityMatrix.
/// </summary>
public enum PickTarget
{
    /// <summary>Standard rocks and small boulders. Breakable at any pickaxe level.</summary>
    SmallRock,

    /// <summary>Large boulder. Requires Steel pickaxe or better.</summary>
    LargeBoulder,

    /// <summary>Meteorite. Requires Gold pickaxe or better.</summary>
    Meteorite,
}
