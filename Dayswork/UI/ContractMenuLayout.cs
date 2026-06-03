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
    private const int PreferredHeight = 720;

    /// <summary>Fixed page height, clamped so it still fits very small viewports.</summary>
    public static int Height => Math.Min(PreferredHeight, Math.Max(480, Game1.uiViewport.Height - 48));

    public static Vector2 GetTopLeft(int width, int height) =>
        Utility.getTopLeftPositionForCenteringOnScreen(width, height);
}
