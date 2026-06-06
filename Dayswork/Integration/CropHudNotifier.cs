using System;
using System.Collections.Generic;
using Dayswork.Core.Crops;
using StardewValley;

namespace Dayswork.Integration;

/// <summary>
/// i18n-backed HUD notices for managed-crop shift behavior (U-MC-05). Purchase / store-fallback /
/// festival notices are added in U-MC-06. Notices are deduplicated per shift so a repeated cause
/// (e.g. the same missing tool across many tiles) only surfaces once.
/// </summary>
internal static class CropHudNotifier
{
    private static bool _toolSkipShown;
    private static bool _fertilizerUnavailableShown;
    private static readonly HashSet<string> _wontGrowShownCrops = new(StringComparer.Ordinal);

    /// <summary>Reset the per-shift dedup flags. Call at shift start.</summary>
    internal static void ResetForShift()
    {
        _toolSkipShown = false;
        _fertilizerUnavailableShown = false;
        _wontGrowShownCrops.Clear();
    }

    internal static void CropWontGrowInTime(string cropName)
    {
        if (!_wontGrowShownCrops.Add(cropName))
            return;
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.crop_wont_grow", new { crop = cropName }),
            HUDMessage.error_type));
    }

    internal static void ToolSkip(ManagedCropActionKind kind)
    {
        if (_toolSkipShown)
            return;
        _toolSkipShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.crop_tool_skip"), HUDMessage.error_type));
    }

    internal static void FertilizerUnavailable()
    {
        if (_fertilizerUnavailableShown)
            return;
        _fertilizerUnavailableShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.crop_fertilizer_unavailable"), HUDMessage.error_type));
    }
}
