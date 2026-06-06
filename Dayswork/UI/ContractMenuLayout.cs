using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.UI;

/// <summary>
/// Shared fixed dimensions for every hub/spoke page in the contract flow so the window never
/// resizes as the player moves between pages or changes selections. The same height value is used
/// by all pages (clamped to the viewport on tiny screens), keeping them visually consistent.
/// The full-screen <see cref="ZoneDrawMenu"/> farm-map overlay is not one of these pages.
/// </summary>
internal static class ContractMenuLayout
{
    public const int Width = 760;
    private const int PreferredWideWidth = 1080;
    private const int PreferredHeight = 720;
    private const int PreferredManageCropsHeight = 820;

    /// <summary>Fixed page height, clamped so it still fits very small viewports.</summary>
    public static int Height => Math.Min(PreferredHeight, Math.Max(480, Game1.uiViewport.Height - 48));

    /// <summary>Wider page width for text-heavy Manage Crops pages, clamped to the viewport.</summary>
    public static int ManageCropsWidth =>
        Math.Min(PreferredWideWidth, Math.Max(Width, Game1.uiViewport.Width - 48));

    /// <summary>Taller Manage Crops page height so four crop groups fit before scrolling.</summary>
    public static int ManageCropsHeight =>
        Math.Min(PreferredManageCropsHeight, Math.Max(Height, Game1.uiViewport.Height - 16));

    public static Vector2 GetTopLeft(int width, int height) =>
        Utility.getTopLeftPositionForCenteringOnScreen(width, height);
}
