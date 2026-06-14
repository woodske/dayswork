namespace Dayswork.Core.Crops;

/// <summary>
/// Authoring-time tag describing how a crop's seeds can be sourced. Display-only in the
/// Manage Crops authoring UI; actual purchasing exactness is resolved at runtime
///.
/// </summary>
public enum CropSupplyTag
{
    /// <summary>Seeds are stocked at Pierre and/or JojaMart, so the farmhand can buy them.</summary>
    AutoBuyable,

    /// <summary>Seeds are not stocked at any store; the farmhand plants them only from input-chest stock.</summary>
    ChestSupplyOnly,
}
