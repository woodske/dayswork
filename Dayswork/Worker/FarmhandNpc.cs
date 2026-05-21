using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.Worker;

internal sealed class FarmhandNpc : NPC
{
    // Placeholder sprite uses the vanilla Marnie texture — custom art is post-v1 (FR-NPC-01).
    internal const string PlaceholderSpritePath   = "Characters\\Marnie";
    internal const string PlaceholderPortraitPath = "Portraits\\Marnie";
    internal const string InternalName            = "DaysworkFarmhand";

    // Required by Stardew Valley's XML serializer. The game should never reach this path
    // because OnSaving removes the NPC before the save is written.
    public FarmhandNpc() { }

    public FarmhandNpc(Vector2 spawnPixelPosition)
        : base(
            new AnimatedSprite(PlaceholderSpritePath, 0, 16, 32),
            spawnPixelPosition,
            2,
            InternalName)
    {
        this.displayName = I18nHelper.Get("npc.farmhand.name");
        this.Portrait = Game1.content.Load<Texture2D>(PlaceholderPortraitPath);
        this.AllowDynamicAppearance = false;
        this.IsInvisible = false;
        this.HideShadow = false;
    }

    public void StopTaskAnimation()
    {
        this.Sprite.ClearAnimation();
        this.Sprite.StopAnimation();
        this.Sprite.CurrentFrame = IdleFrameFor(this.FacingDirection);
    }

    public void FaceTaskDirection(int direction)
    {
        this.faceDirection(direction);
        this.Sprite.CurrentFrame = IdleFrameFor(direction);
    }

    internal static int IdleFrameFor(int facingDirection) =>
        facingDirection switch
        {
            0 => 8,
            1 => 4,
            2 => 0,
            3 => 12,
            _ => 0,
        };
}
