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

    private int _staminaRemaining;
    private int _staminaCapacity;

    public void SetStamina(int remaining, int capacity)
    {
        _staminaRemaining = Math.Max(0, remaining);
        _staminaCapacity = Math.Max(0, capacity);
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

    public override void drawAboveAlwaysFrontLayer(SpriteBatch b)
    {
        base.drawAboveAlwaysFrontLayer(b);

        if (_staminaCapacity <= 0)
            return;

        var local = Game1.GlobalToLocal(Game1.viewport, this.Position + new Vector2(0f, -44f));
        const int barWidth = 40;
        const int barHeight = 6;
        var barX = (int)local.X - (barWidth / 2) + 32;
        var barY = (int)local.Y;
        var fillWidth = Math.Clamp((int)Math.Round((double)_staminaRemaining / _staminaCapacity * (barWidth - 2)), 0, barWidth - 2);

        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barWidth, barHeight), Color.Black * 0.8f);
        b.Draw(Game1.staminaRect, new Rectangle(barX + 1, barY + 1, barWidth - 2, barHeight - 2), new Color(50, 34, 18));
        b.Draw(Game1.staminaRect, new Rectangle(barX + 1, barY + 1, fillWidth, barHeight - 2), new Color(145, 214, 68));
    }
}
