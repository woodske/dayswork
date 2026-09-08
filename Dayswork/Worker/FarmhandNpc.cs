using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.Worker;

/// <summary>
/// The farmhand. Driven entirely by its <see cref="Dayswork.Orchestration.ShiftOrchestrator"/> on
/// the host; on every other peer it is inert without any override of ours, because vanilla already
/// gates movement on the host: <c>Character.update</c> runs <c>updateMovement</c> and the
/// <c>controller</c> only when <c>Game1.IsMasterGame</c>, and otherwise calls
/// <c>updateSlaveAnimation</c>, which replays the synced sprite state (verified against the
/// decompile, 2026-09-07 — see <c>docs/game-data/multiplayer-and-ownership.md</c>). A client
/// therefore watches the worker walk and swing without simulating anything.
/// </summary>
internal sealed class FarmhandNpc : NPC
{
    internal const string PlaceholderPortraitPath = "Portraits/Marnie";
    internal const string InternalName            = "DaysworkFarmhand";

    // Per-worker state that has to reach other players lives in the NPC's modData, which is a
    // synced net field — appearance in particular, because it is the whole of what a remote client
    // needs to draw the right sprite (getTextureName below). The worker is never serialized into a
    // save (OnSaving despawns it), so these keys live only as long as the shift.
    internal const string NameModDataKey    = "Bindicle.Dayswork/Name";
    internal const string VariantModDataKey = "Bindicle.Dayswork/Variant";
    internal const string StaminaModDataKey = "Bindicle.Dayswork/Stamina";

    // Required by Stardew Valley's XML serializer. Deliberately inert — no content loads and no
    // appearance work, because the serializer constructs it outside any shift. The game should
    // never reach this path anyway: OnSaving removes the NPC before the save is written.
    public FarmhandNpc() { }

    public FarmhandNpc(Vector2 spawnPixelPosition, Guid officeId, string workerName, string appearanceKey)
        : base(
            new AnimatedSprite(FarmhandAppearance.SpriteAssetFor(appearanceKey), 0, 16, 32),
            spawnPixelPosition,
            2,
            // Unique per office so N concurrent workers never collide in the game's name-based
            // lookups (getCharacterFromName, net sync, serialization guards).
            NameFor(officeId))
    {
        this.modData[NameModDataKey] = workerName ?? "";
        this.modData[VariantModDataKey] = WorkerAppearances.Resolve(appearanceKey).Key;
        this.modData[StaminaModDataKey] = FormatStamina(0, 0);
        this.AllowDynamicAppearance = false;
        this.IsInvisible = false;
        this.HideShadow = false;
    }

    /// <summary>The internal NPC name for the worker of a given office.</summary>
    internal static string NameFor(Guid officeId) => $"{InternalName}_{officeId:N}";

    /// <summary>The player-facing name for a worker: the contract's chosen name, or the generic
    /// localized "Farmhand" when unset. Shared by the NPC display name and HUD notices.</summary>
    internal static string DisplayNameFor(string workerName) =>
        string.IsNullOrWhiteSpace(workerName)
            ? I18nHelper.Get("npc.farmhand.name")
            : workerName;

    // Read from modData rather than the base class's cached field so the name survives net sync,
    // and ignore writes: the name is owned by the contract, and vanilla would otherwise overwrite
    // it with translateName() (the unique per-office NPC name) from reloadSprite.
    public override string displayName
    {
        get => DisplayNameFor(this.modData.TryGetValue(NameModDataKey, out var name) ? name : "");
        set { }
    }

    // The unique per-office Name would otherwise drive vanilla texture resolution:
    // getTextureName() falls back to the NPC name when there's no Data/Characters entry, and
    // ChooseAppearance/reloadSprite build "Characters/…" + "Portraits/…" paths from it. Pin it to
    // this worker's appearance variant so those paths land on the asset the sprite already uses —
    // which also makes ChooseAppearance a no-op instead of a sprite reset.
    public override string getTextureName() =>
        FarmhandAppearance.TextureNameFor(this.modData.TryGetValue(VariantModDataKey, out var key) ? key : null);

    /// <summary>~5% granularity for the published energy value. The bar is 40px wide, so a finer
    /// step is invisible.</summary>
    private const int StaminaSteps = 20;

    public void SetStamina(int remaining, int capacity)
    {
        remaining = Math.Max(0, remaining);
        capacity = Math.Max(0, capacity);

        // Quantise before publishing. This runs once per work beat and modData is a synced net
        // field, so writing the exact figure would put hundreds of updates a day on the wire for
        // changes nobody can see. Empty and full are never rounded away.
        if (capacity > 0 && remaining > 0 && remaining < capacity)
        {
            var step = (double)capacity / StaminaSteps;
            remaining = Math.Clamp((int)Math.Round(Math.Round(remaining / step) * step), 1, capacity - 1);
        }

        var published = FormatStamina(remaining, capacity);
        if (!this.modData.TryGetValue(StaminaModDataKey, out var current) || current != published)
            this.modData[StaminaModDataKey] = published;
    }

    private static string FormatStamina(int remaining, int capacity) => $"{remaining}/{capacity}";

    private bool TryReadStamina(out int remaining, out int capacity)
    {
        remaining = 0;
        capacity = 0;

        if (!this.modData.TryGetValue(StaminaModDataKey, out var raw))
            return false;

        var separator = raw.IndexOf('/');
        return separator > 0
            && int.TryParse(raw.AsSpan(0, separator), out remaining)
            && int.TryParse(raw.AsSpan(separator + 1), out capacity);
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

        if (!TryReadStamina(out var remaining, out var capacity) || capacity <= 0)
            return;

        var local = Game1.GlobalToLocal(Game1.viewport, this.Position + new Vector2(0f, -80f));
        const int barWidth = 40;
        const int barHeight = 6;
        var barX = (int)local.X - (barWidth / 2) + 32;
        var barY = (int)local.Y;
        var fillWidth = Math.Clamp((int)Math.Round((double)remaining / capacity * (barWidth - 2)), 0, barWidth - 2);

        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barWidth, barHeight), Color.Black * 0.8f);
        b.Draw(Game1.staminaRect, new Rectangle(barX + 1, barY + 1, barWidth - 2, barHeight - 2), new Color(50, 34, 18));
        b.Draw(Game1.staminaRect, new Rectangle(barX + 1, barY + 1, fillWidth, barHeight - 2), new Color(145, 214, 68));
    }
}
