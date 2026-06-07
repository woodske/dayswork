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
    private static bool _fallbackStoreShown;
    private static bool _festivalSkippedShown;
    private static bool _insufficientFundsShown;
    private static bool _shoppingUnavailableShown;
    private static bool _shoppingDepartureShown;
    private static bool _shoppingWaitingShown;
    private static bool _shoppingReturningShown;
    private static readonly HashSet<string> _wontGrowShownCrops = new(StringComparer.Ordinal);

    /// <summary>Reset the per-shift dedup flags. Call at shift start.</summary>
    internal static void ResetForShift()
    {
        _toolSkipShown = false;
        _fertilizerUnavailableShown = false;
        _fallbackStoreShown = false;
        _festivalSkippedShown = false;
        _insufficientFundsShown = false;
        _shoppingUnavailableShown = false;
        _shoppingDepartureShown = false;
        _shoppingWaitingShown = false;
        _shoppingReturningShown = false;
        _wontGrowShownCrops.Clear();
    }

    /// <summary>One notice per purchase line (FR-MC-19) — not deduplicated.</summary>
    internal static void PurchaseCompleted(string itemName, int quantity)
    {
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.bought_item", new { count = quantity, item = itemName }),
            HUDMessage.newQuest_type));
    }

    internal static void UsingFallbackStore(string closedStoreName)
    {
        if (_fallbackStoreShown)
            return;
        _fallbackStoreShown = true;
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.using_fallback_store", new { store = closedStoreName }),
            HUDMessage.newQuest_type));
    }

    internal static void ShoppingFestivalSkipped()
    {
        if (_festivalSkippedShown)
            return;
        _festivalSkippedShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.shopping_festival_skipped"), HUDMessage.error_type));
    }

    internal static void InsufficientFunds()
    {
        if (_insufficientFundsShown)
            return;
        _insufficientFundsShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.shopping_insufficient_funds"), HUDMessage.error_type));
    }

    internal static void ShoppingUnavailable()
    {
        if (_shoppingUnavailableShown)
            return;
        _shoppingUnavailableShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.shopping_unavailable"), HUDMessage.error_type));
    }

    internal static void ShoppingDeparture()
    {
        if (_shoppingDepartureShown)
            return;
        _shoppingDepartureShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.shopping_departing"), HUDMessage.newQuest_type));
    }

    internal static void WaitingForStoreOpen(string storeName)
    {
        if (_shoppingWaitingShown)
            return;
        _shoppingWaitingShown = true;
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.shopping_waiting", new { store = storeName }),
            HUDMessage.newQuest_type));
    }

    internal static void ShoppingPurchaseSummary(int count, int gold)
    {
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.shopping_purchase_summary", new { count, gold }),
            HUDMessage.newQuest_type));
    }

    internal static void ShoppingReturning()
    {
        if (_shoppingReturningShown)
            return;
        _shoppingReturningShown = true;
        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.shopping_returning"), HUDMessage.newQuest_type));
    }

    internal static void ShoppingDeposited(int count)
    {
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.shopping_deposited", new { count }),
            HUDMessage.newQuest_type));
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
