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
/// data-driven way to gate the overlay on <see cref="HiringBuilding.WorkCompletedToday"/>.
/// Single-player only (the flag is not synced/persisted).
/// </summary>
internal sealed class HiringBuildingOverlayRenderer
{
    private Texture2D? _texture;

    public void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!HiringBuilding.WorkCompletedToday)
            return;
        if (Game1.currentLocation is not Farm farm)
            return;

        var building = HiringBuildingInteraction.FindHiringBuilding(farm);
        if (building is null)
            return;

        var texture = _texture ??= Game1.content.Load<Texture2D>(HiringBuilding.TextureAsset);
        var b = e.SpriteBatch;

        // Top-left of the base sprite in world pixels: the sprite's bottom aligns with the bottom
        // of the footprint, drawn at the game's 4x pixel zoom.
        float worldX = building.tileX.Value * 64f;
        float worldY = (building.tileY.Value + HiringBuilding.TilesHigh) * 64f
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
