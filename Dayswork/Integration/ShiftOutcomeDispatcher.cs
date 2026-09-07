using System;
using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Dayswork.Integration;

// Missed/overflow items are deposited into the hiring building's static output chest (falling back
// to the SPONSOR's shipping bin so nothing is ever lost). Money and notices follow the sponsor too:
// festival one-time refunds are credited to the owner's wallet, and a notice is a HUD message when
// the owner is the player at this screen and a log line otherwise.
internal sealed class ShiftOutcomeDispatcher : IShiftOutcomeDispatcher
{
    public void DispatchOverflowDelivery(
        StardewValley.Buildings.Building? office,
        long ownerId,
        IReadOnlyList<ItemStack> items,
        IReadOnlyList<OverflowCategory> categories,
        IReadOnlyDictionary<string, SObject> flavorTemplates)
    {
        if (items.Count == 0) return;

        var built = BuildItems(items, flavorTemplates);
        if (built.Count == 0) return;

        var deposited = DepositToBuildingChestOrBin(office, ownerId, built, out var usedChest, out var chestWasFull);

        var destination = usedChest && !chestWasFull ? "the farmhand office chest"
                        : chestWasFull               ? "the farmhand office chest (partial) + shipping bin"
                        :                              "the shipping bin";
        ModEntry.ModMonitor.Log(
            $"[Dayswork] Deposited {deposited} overflow item stack(s) to {destination}.",
            LogLevel.Trace);

        var notifyKey = usedChest && !chestWasFull ? "notify.items_deposited_chest"
                      : chestWasFull               ? "notify.items_deposited_chest_overflow"
                      :                              "notify.items_deposited_bin";
        ShowInfo(ownerId, I18nHelper.Get(notifyKey));
    }

    public void ShowCannotAffordNotice(Contract contract, int dailyPrice, int shortfall)
    {
        ShowError(contract.OwnerId, I18nHelper.Get("notify.cannot_afford", new { price = dailyPrice, shortfall }));
    }

    public void ShowContractLostNotice(long ownerId)
    {
        ShowError(ownerId, I18nHelper.Get("notify.contract_lost"));
    }

    public void ShowNeedsAttentionNotice(Contract contract)
    {
        ShowError(contract.OwnerId, I18nHelper.Get("notify.needs_attention"));
    }

    public void ShowFestivalNotice(Contract contract, int refundGold)
    {
        if (refundGold > 0)
        {
            Sponsor.Credit(contract.OwnerId, refundGold);
            ShowInfo(contract.OwnerId, I18nHelper.Get("notify.festival_refund", new { refund = refundGold }));
        }
        else
        {
            ShowInfo(contract.OwnerId, I18nHelper.Get("notify.festival"));
        }
    }

    // Deposits each item stack into the building output chest; any leftover (chest full or missing)
    // goes to the sponsor's shipping bin. Returns the number of stacks handled and whether the chest
    // was used. chestWasFull is set when the chest was present but addItem returned a remainder, so
    // the caller can show a fallback-to-bin notice distinct from the "no chest assigned" case.
    private static int DepositToBuildingChestOrBin(
        StardewValley.Buildings.Building? office,
        long ownerId,
        List<Item> items,
        out bool usedChest,
        out bool chestWasFull)
    {
        usedChest = false;
        chestWasFull = false;
        var chest = office?.GetBuildingChest(HiringBuilding.OutputChestId);
        var bin = Sponsor.ShippingBin(Game1.getFarm(), ownerId);

        var count = 0;
        foreach (var item in items)
        {
            Item? leftover = item;
            if (chest is not null)
            {
                leftover = chest.addItem(item);
                if (leftover is null)
                    usedChest = true;
                else
                    chestWasFull = true;
            }

            if (leftover is not null && bin is not null)
                bin.Add(leftover);

            count++;
        }

        return count;
    }

    private static void ShowInfo(long ownerId, string text) =>
        Show(ownerId, text, HUDMessage.newQuest_type);

    private static void ShowError(long ownerId, string text) =>
        Show(ownerId, text, HUDMessage.error_type);

    // A notice belongs to the contract's owner. Shown on the HUD when that is the player at this
    // screen; logged otherwise, until Phase 4 adds delivery to a remote owner.
    private static void Show(long ownerId, string text, int hudType)
    {
        if (Sponsor.IsLocal(ownerId))
        {
            Game1.addHUDMessage(new HUDMessage(text, hudType));
            return;
        }

        ModEntry.ModMonitor.Log($"[Dayswork] Notice for owner {ownerId}: {text}", DevLog.WarnLevel);
    }

    private static List<Item> BuildItems(IReadOnlyList<ItemStack> stacks, IReadOnlyDictionary<string, SObject> flavorTemplates)
    {
        var result = new List<Item>(stacks.Count);
        foreach (var s in stacks)
        {
            if (string.IsNullOrWhiteSpace(s.QualifiedItemId))
                continue;

            // A captured flavored/colored item (roe, wine…) is cloned from its template so identity
            // and price survive; everything else is the ordinary id-based create.
            Item? item = s.FlavorId is { } flavorId
                ? Orchestration.FlavorItemRegistry.Rebuild(flavorTemplates, flavorId, s.Quantity, s.Quality)
                : null;
            item ??= ItemRegistry.Create(s.QualifiedItemId, s.Quantity);

            if (item is null || IsErrorItem(item))
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Could not create overflow item '{s.QualifiedItemId}' x{s.Quantity}; skipped.",
                    DevLog.WarnLevel);
                continue;
            }
            if (item is SObject sObj && s.FlavorId is null && s.Quality > 0)
                sObj.Quality = s.Quality;

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
