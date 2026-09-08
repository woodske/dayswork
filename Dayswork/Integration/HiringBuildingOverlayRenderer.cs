using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Integration;

/// <summary>
/// Draws the hiring building's "worker done for the day" flair — warm-lit windows/lantern (after
/// dusk) and an animated chimney smoke loop — on top of the base building sprite.
///
/// This is done in C# rather than via Data/Buildings <c>DrawLayers</c> because BuildingDrawLayer
/// has no GameStateQuery condition in this game version (layers always draw), so there is no
/// data-driven way to gate the overlay on "this office's worker is done".
///
/// Each office lights independently: the flag is the building's own
/// <see cref="OfficeModData.DoneOnKey"/> day stamp, so three offices whose workers finish at
/// different times light one by one — and, being modData, every connected player sees the same
/// thing.
/// </summary>
internal sealed class HiringBuildingOverlayRenderer
{
    public void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (Game1.currentLocation is not Farm farm)
            return;

        foreach (var office in OfficeResolver.EnumerateOffices(farm))
        {
            if (!OfficeModData.IsDoneToday(office))
                continue;

            // The office's own texture, not a fresh load of the raw sheet: when the office has been
            // repainted this is vanilla's painted copy, so the glow and smoke drawn from it agree
            // with the walls underneath. Each office keeps its own, so two differently painted
            // offices light correctly. (The glow and smoke frames are left out of the paint mask,
            // so they read the same either way — but the texture is per-building, and this is the
            // one that is always the right one.)
            var texture = office.texture?.Value;
            if (texture is null)
                continue;

            DrawDoneForTheDay(e.SpriteBatch, texture, office);
        }
    }

    private static void DrawDoneForTheDay(SpriteBatch b, Texture2D texture, StardewValley.Buildings.Building office)
    {
        // Top-left of the base sprite in world pixels: the sprite's bottom aligns with the bottom
        // of the footprint, drawn at the game's 4x pixel zoom.
        float worldX = office.tileX.Value * 64f;
        float worldY = (office.tileY.Value + HiringBuilding.TilesHigh) * 64f
                       - HiringBuilding.SpriteHeightPx * 4f;
        var origin = Game1.GlobalToLocal(Game1.viewport, new Vector2(worldX, worldY));

        // RenderedWorld draws after the world (deferred batch), so these land on top of the
        // building. Lit windows/lantern only read right after dusk.
        if (Game1.timeOfDay >= HiringBuilding.GlowStartTime)
        {
            b.Draw(texture, origin, HiringBuilding.GlowSourceRect, Color.White,
                0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
        }

        // Chimney smoke: advance the 4-frame loop by wall-clock time.
        double ms = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0d;
        int frame = (int)(ms / HiringBuilding.SmokeFrameDurationMs) % HiringBuilding.SmokeFrameCount;
        var smokeSrc = HiringBuilding.SmokeFirstFrame;
        smokeSrc.X += frame * HiringBuilding.SmokeFramePx;
        var smokePos = origin + HiringBuilding.SmokeDrawOffset * 4f;
        b.Draw(texture, smokePos, smokeSrc, Color.White * 0.85f,
            0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
    }
}
