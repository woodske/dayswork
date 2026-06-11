using System;
using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Dayswork.Integration;

// Missed/overflow items are deposited into the hiring building's static output chest
// (falling back to the shipping bin so nothing is ever lost), and text notices are shown as
// in-game HUD messages. Festival one-time refunds are credited directly to the player's gold.
internal sealed class ShiftOutcomeDispatcher : IShiftOutcomeDispatcher
{
    public void DispatchOverflowDelivery(IReadOnlyList<ItemStack> items, IReadOnlyList<OverflowCategory> categories)
    {
        if (items.Count == 0) return;

        var built = BuildItems(items);
        if (built.Count == 0) return;

        var deposited = DepositToBuildingChestOrBin(built, out var usedChest);

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Deposited {deposited} overflow item stack(s) to {(usedChest ? "the farmhand cabin chest" : "the shipping bin")}.",
            LogLevel.Trace);

        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get(usedChest ? "notify.items_deposited_chest" : "notify.items_deposited_bin"),
            HUDMessage.newQuest_type));
    }

    public void ShowCannotAffordNotice(Contract contract, int dailyPrice, int shortfall)
    {
        ShowError(I18nHelper.Get("notify.cannot_afford", new { price = dailyPrice, shortfall }));
    }

    public void ShowNeedsAttentionNotice(Contract contract)
    {
        ShowError(I18nHelper.Get("notify.needs_attention"));
    }

    public void ShowFestivalNotice(Contract contract, int refundGold)
    {
        if (refundGold > 0 && Game1.player is not null)
        {
            Game1.player.Money += refundGold;
            ShowInfo(I18nHelper.Get("notify.festival_refund", new { refund = refundGold }));
        }
        else
        {
            ShowInfo(I18nHelper.Get("notify.festival"));
        }
    }

    // Deposits each item stack into the building output chest; any leftover (chest full or missing)
    // goes to the shipping bin. Returns the number of stacks handled and whether the chest was used.
    private static int DepositToBuildingChestOrBin(List<Item> items, out bool usedChest)
    {
        usedChest = false;
        var chest = Game1.getFarm() is { } farm ? HiringBuilding.TryGetOutputChest(farm) : null;
        var bin = Game1.getFarm()?.getShippingBin(Game1.player);

        var count = 0;
        foreach (var item in items)
        {
            Item? leftover = item;
            if (chest is not null)
            {
                leftover = chest.addItem(item);
                if (leftover is null)
                    usedChest = true;
            }

            if (leftover is not null && bin is not null)
                bin.Add(leftover);

            count++;
        }

        return count;
    }

    private static void ShowInfo(string text) =>
        Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));

    private static void ShowError(string text) =>
        Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));

    private static List<Item> BuildItems(IReadOnlyList<ItemStack> stacks)
    {
        var result = new List<Item>(stacks.Count);
        foreach (var s in stacks)
        {
            if (string.IsNullOrWhiteSpace(s.QualifiedItemId))
                continue;

            var item = ItemRegistry.Create(s.QualifiedItemId, s.Quantity);
            if (item is null || IsErrorItem(item))
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Could not create overflow item '{s.QualifiedItemId}' x{s.Quantity}; skipped.",
                    DevLog.WarnLevel);
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    // ItemRegistry.Create(badId) returns SDV's fallback Error Item rather than null.
    private static bool IsErrorItem(Item item) =>
        string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);
}
