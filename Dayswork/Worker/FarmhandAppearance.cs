using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Worker;

/// <summary>
/// Serves the worker's sprite sheet and its colour variants.
///
/// A variant is not a hand-drawn sheet: it is the one base sheet run through the game's own
/// <see cref="BuildingPainter"/> with an authored mask (<c>assets/farmhand_PaintMask.png</c>, whose
/// pure red / lime / blue pixels mark the cap, shirt and overalls). Each variant is registered as
/// its own content asset — <c>Characters/DaysworkFarmhand_&lt;key&gt;</c> — so the worker's appearance
/// travels as nothing but a texture name: <see cref="FarmhandNpc.getTextureName"/> derives it from
/// the NPC's synced <c>modData</c>, and every client resolves the same deterministic asset with no
/// custom sync of its own.
/// </summary>
internal static class FarmhandAppearance
{
    /// <summary>The paint mask asset. Named under <c>Characters/</c> because that is where the
    /// sheet it masks lives; it is never used as an NPC texture.</summary>
    internal const string MaskAssetName = "Characters/DaysworkFarmhand_PaintMask";

    private const string SheetFile = "assets/farmhand.png";
    private const string MaskFile = "assets/farmhand_PaintMask.png";

    /// <summary>The NPC texture name for an appearance key — i.e. what
    /// <c>getTextureName()</c> returns, which vanilla turns into <c>Characters/…</c> and
    /// <c>Portraits/…</c>. The default palette resolves to the plain base sheet rather than to a
    /// painted variant of itself.</summary>
    internal static string TextureNameFor(string? appearanceKey)
    {
        var appearance = WorkerAppearances.Resolve(appearanceKey);
        return appearance.HasTint
            ? $"{FarmhandNpc.InternalName}_{appearance.Key}"
            : FarmhandNpc.InternalName;
    }

    /// <summary>The sprite-sheet asset name for an appearance key, for anything loading the sheet
    /// directly (the worker's <c>AnimatedSprite</c>, the Preferences preview).</summary>
    internal static string SpriteAssetFor(string? appearanceKey) => $"Characters/{TextureNameFor(appearanceKey)}";

    /// <summary>Handles the AssetRequested events for the worker sheet, its paint mask, every
    /// painted variant, and the (shared) portrait.</summary>
    internal static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo($"Characters/{FarmhandNpc.InternalName}"))
        {
            e.LoadFromModFile<Texture2D>(SheetFile, AssetLoadPriority.Medium);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(MaskAssetName))
        {
            e.LoadFromModFile<Texture2D>(MaskFile, AssetLoadPriority.Medium);
            return;
        }

        // Portrait still uses Marnie until a custom portrait is ready — one placeholder for every
        // variant, since only the body sheet is recoloured (docs/game-data/farmhand-art.md).
        if (e.NameWithoutLocale.IsEquivalentTo($"Portraits/{FarmhandNpc.InternalName}"))
        {
            e.LoadFrom(LoadPlaceholderPortrait, AssetLoadPriority.Medium);
            return;
        }

        foreach (var appearance in WorkerAppearances.All)
        {
            if (!appearance.HasTint)
                continue;

            if (e.NameWithoutLocale.IsEquivalentTo($"Characters/{FarmhandNpc.InternalName}_{appearance.Key}"))
            {
                var requested = appearance;
                e.LoadFrom(() => Paint(requested), AssetLoadPriority.Medium);
                return;
            }

            if (e.NameWithoutLocale.IsEquivalentTo($"Portraits/{FarmhandNpc.InternalName}_{appearance.Key}"))
            {
                e.LoadFrom(LoadPlaceholderPortrait, AssetLoadPriority.Medium);
                return;
            }
        }
    }

    private static Texture2D LoadPlaceholderPortrait() =>
        Game1.content.Load<Texture2D>(FarmhandNpc.PlaceholderPortraitPath);

    private static Texture2D Paint(WorkerAppearance appearance)
    {
        var sheet = Game1.content.Load<Texture2D>($"Characters/{FarmhandNpc.InternalName}");
        var painted = BuildingPainter.Apply(sheet, MaskAssetName, ToPaintColor(appearance));
        if (painted is not null)
            return painted;

        // Apply returns null when the mask can't be loaded. Fall back to the base look rather than
        // to no sprite at all — but hand back a copy, never the content manager's own instance for
        // another asset, which this asset would then own and dispose.
        ModEntry.ModMonitor.Log(
            $"[Dayswork] Could not paint farmhand appearance '{appearance.Key}' (paint mask missing or unreadable); using the default sheet.",
            DevLog.WarnLevel);
        return Copy(sheet);
    }

    private static BuildingPaintColor ToPaintColor(WorkerAppearance appearance)
    {
        var color = new BuildingPaintColor();
        Fill(appearance.Cap, color.Color1Default, color.Color1Hue, color.Color1Saturation, color.Color1Lightness);
        Fill(appearance.Shirt, color.Color2Default, color.Color2Hue, color.Color2Saturation, color.Color2Lightness);
        Fill(appearance.Overalls, color.Color3Default, color.Color3Hue, color.Color3Saturation, color.Color3Lightness);
        return color;

        static void Fill(AppearanceTint? tint, NetBool isDefault, NetInt hue, NetInt saturation, NetInt lightness)
        {
            if (tint is not { } value)
                return;

            isDefault.Value = false;
            hue.Value = value.Hue;
            saturation.Value = value.Saturation;
            lightness.Value = value.Lightness;
        }
    }

    private static Texture2D Copy(Texture2D source)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);

        var copy = new Texture2D(Game1.graphics.GraphicsDevice, source.Width, source.Height);
        copy.SetData(pixels);
        return copy;
    }
}
