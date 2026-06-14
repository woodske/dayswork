namespace Dayswork.Core.Compat;

/// <summary>
/// Stable identity for a farm map, computed from the live map. SVE farm packs replace the "Farm"
/// location, so the location name cannot distinguish them; the map's dimensions (plus an optional
/// unique map-property <see cref="Discriminator"/>) identify the map Content Patcher actually
/// applied — correct even when multiple farm-map packs are installed.
/// </summary>
public readonly record struct FarmMapSignature(int Width, int Height, string Discriminator)
{
    /// <summary>Creates a signature with no discriminator (dimensions only).</summary>
    public FarmMapSignature(int width, int height) : this(width, height, string.Empty)
    {
    }
}
