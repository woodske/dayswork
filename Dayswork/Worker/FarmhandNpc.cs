using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.Worker;

internal sealed class FarmhandNpc : NPC
{
    // Placeholder sprite uses the vanilla Marnie texture — custom art is post-v1 (FR-NPC-01, Q9).
    internal const string PlaceholderSpritePath   = "Characters\\Marnie";
    internal const string PlaceholderPortraitPath = "Portraits\\Marnie";
    // Internal NPC name — must be stable and unique. SV uses this to build the portrait asset
    // path, so it must match whatever ModEntry.OnAssetRequested redirects.
    internal const string InternalName = "DaysworkFarmhand";

    // Required by Stardew Valley's XML serializer. The game should never reach this path
    // because OnSaving removes the NPC before the save is written, but it prevents a crash
    // if cleanup is skipped for any reason.
    public FarmhandNpc() { }

    public FarmhandNpc(Vector2 spawnPixelPosition)
        : base(
            new AnimatedSprite(PlaceholderSpritePath, 0, 16, 32),
            spawnPixelPosition,
            2,  // facing direction: down
            InternalName)
    {
        this.displayName = I18nHelper.Get("npc.farmhand.name");
        this.Portrait = Game1.content.Load<Texture2D>(PlaceholderPortraitPath);
        this.AllowDynamicAppearance = false;
        this.IsInvisible = false;
        this.HideShadow = false;
    }
    // Invulnerability override deferred to U-13 (FR-NPC-02).
}
