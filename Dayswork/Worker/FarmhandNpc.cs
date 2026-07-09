using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.Worker;

internal sealed class FarmhandNpc : NPC
{
    internal const string SpritePath         = "Characters\\DaysworkFarmhand";
    internal const string PlaceholderPortraitPath = "Portraits\\Marnie";
    internal const string InternalName            = "DaysworkFarmhand";

    // Required by Stardew Valley's XML serializer. The game should never reach this path
    // because OnSaving removes the NPC before the save is written.
    public FarmhandNpc() { }

    public FarmhandNpc(Vector2 spawnPixelPosition, ContractId contractId, string workerName)
        : base(
            new AnimatedSprite(SpritePath, 0, 16, 32),
            spawnPixelPosition,
            2,
            // Unique per contract so N concurrent workers never collide in name-based game
            // lookups (getCharacterFromName, net sync, serialization guards).
            $"{InternalName}_{contractId.Value:N}")
    {
        this.displayName = DisplayNameFor(workerName);
        this.Portrait = Game1.content.Load<Texture2D>(PlaceholderPortraitPath);
        this.AllowDynamicAppearance = false;
        this.IsInvisible = false;
        this.HideShadow = false;
    }

    /// <summary>The player-facing name for a worker: the contract's chosen name, or the generic
    /// localized "Farmhand" when unset. Shared by the NPC display name and HUD notices.</summary>
    internal static string DisplayNameFor(string workerName) =>
        string.IsNullOrWhiteSpace(workerName)
            ? I18nHelper.Get("npc.farmhand.name")
            : workerName;

    // The unique per-contract Name would otherwise drive vanilla texture resolution:
    // getTextureName() falls back to the NPC name when there's no Data/Characters entry, and
    // ChooseAppearance/reloadSprite build "Characters/…" + "Portraits/…" paths from it. Pin it
    // to the shared asset name so every worker loads the one farmhand sprite/portrait.
    public override string getTextureName() => InternalName;

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

        var local = Game1.GlobalToLocal(Game1.viewport, this.Position + new Vector2(0f, -80f));
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
