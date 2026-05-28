using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Integration.MailFramework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Integration;

// M-16 MailDispatcher (Patterns P + U). Sends the single per-shift settlement letter (overflow items
    // + refund gold), plus the text-only cannot-afford / needs-attention / festival notices.
    // All user-visible text is routed through I18nHelper (UX-U15-01). No new Harmony patches
    // (NFR-MAINT-04).
//
// Refund gold rides the letter as a credit-on-collection callback (DEV-U15-04 / BR-REF-04):
// settlement refunds arrive next morning; one-time festival refunds ride the same-day no-worker
// notice. If the mail backend is unavailable, items fall back to the shipping bin and gold is
// credited directly so nothing is lost (SAFE-U15-01 / REL-U15-04).
internal sealed class MailDispatcher : IMailDispatcher
{
    private const string PendingSettlementsKey = "Dayswork.PendingSettlements";

    private MailFrameworkModApiAdapter? _mfm;
    private readonly IDataHelper _dataHelper;
    private readonly List<PendingSettlementRecord> _pendingSettlements = new();

    private enum DeliveryTiming
    {
        Today,
        Tomorrow,
    }

    internal MailDispatcher(IDataHelper dataHelper, object? mfm = null)
    {
        _dataHelper = dataHelper;
        if (mfm is not null)
            SetApi(mfm);
    }

    // Mod-provided APIs must be fetched after all mods initialize (SMAPI GameLaunched), not in
    // Entry(); ModEntry injects the MFM API here once it's available.
    internal void SetApi(object? mfm)
    {
        if (mfm is null)
        {
            _mfm = null;
            return;
        }

        try
        {
            _mfm = new MailFrameworkModApiAdapter(mfm);
        }
        catch (Exception ex)
        {
            _mfm = null;
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Mail Framework Mod API shape was not recognized: {ex.Message}",
                LogLevel.Warn);
        }
    }

    // Re-registers any settlement letters that were queued during the previous session's Saving
    // event but were lost from MFM's registry because MFM (a required dependency) saves its state
    // before Dayswork's Saving handler fires. Called at SaveLoaded, after GameLaunched has already
    // wired up the MFM API, so _mfm is available here.
    //
    // MFM also fires its SaveLoaded handler BEFORE ours (dependency load order), so its own
    // morning delivery check runs before we re-register the letter. To guarantee the player
    // receives the settlement on the correct morning we push the letter directly into
    // Game1.mailbox — MFM will still serve the items and callback when the letter is opened.
    internal void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var saved = _dataHelper.ReadSaveData<List<PendingSettlementRecord>>(PendingSettlementsKey)
            ?? new List<PendingSettlementRecord>();

        _pendingSettlements.Clear();
        foreach (var record in saved)
        {
            if (Game1.player?.mailReceived.Contains(record.Id) == true)
                continue;

            _pendingSettlements.Add(record);
            ReRegisterWithMfm(record);
            AddToMailboxToday(record.Id);
        }

        // Write back immediately to prune any already-delivered entries from saved data.
        _dataHelper.WriteSaveData(PendingSettlementsKey, _pendingSettlements);
    }

    public void QueueSettlement(IReadOnlyList<ItemStack> items, IReadOnlyList<OverflowCategory> categories, int refundGold)
    {
        bool hasItems = items.Count > 0;
        bool hasGold  = refundGold > 0;
        if (!hasItems && !hasGold) return;

        var attachments = hasItems ? BuildItems(items) : new List<Item>();
        if (attachments.Count == 0 && !hasGold)
        {
            ModEntry.ModMonitor.Log(
                "[Dayswork] Settlement mail had no valid item attachments and no refund; suppressing empty letter.",
                LogLevel.Warn);
            return;
        }

        var sender = I18nHelper.Get("mail.sender");
        var body   = BuildSettlementBody(categories, refundGold, attachments.Count > 0);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue settlement letter attachments={attachments.Count} refund={refundGold} categories={string.Join(",", categories.Select(category => $"{category.Reason}:{category.ScopeFamily}:{category.ScopeName}"))}.",
            LogLevel.Debug);
        if (TrySendViaMfm(
            $"Dayswork.Settlement.{CurrentDay()}.{Guid.NewGuid():N}",
            sender,
            body,
            attachments,
            refundGold,
            DeliveryTiming.Tomorrow))
            return;

        // Last-resort safety net (SAFE-U15-01 / REL-U15-04): never lose items or refunds.
        ModEntry.ModMonitor.Log(
            "[Dayswork] Settlement mail could not be sent via Mail Framework Mod; depositing items in the shipping bin and crediting the refund directly so nothing is lost.",
            LogLevel.Warn);
        if (attachments.Count > 0)
        {
            var bin = Game1.getFarm().getShippingBin(Game1.player);
            foreach (var item in attachments)
                bin.Add(item);
        }
        if (hasGold && Game1.player is not null)
            Game1.player.Money += refundGold;
    }

    public void QueueCannotAffordNotice(Contract contract, int dailyPrice, int shortfall)
    {
        var sender = I18nHelper.Get("mail.sender");
        var body   = I18nHelper.Get("mail.cannot_afford.body", new { price = dailyPrice, shortfall });

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue cannot-afford notice price={dailyPrice} shortfall={shortfall}.",
            LogLevel.Debug);
        if (!TrySendViaMfm(
            $"Dayswork.CannotAfford.{CurrentDay()}.{Guid.NewGuid():N}",
            sender,
            body,
            new List<Item>(),
            0,
            DeliveryTiming.Today))
            ModEntry.ModMonitor.Log(
                "[Dayswork] Cannot-afford notice could not be sent (Mail Framework Mod unavailable).",
                LogLevel.Warn);
    }

    public void QueueNeedsAttentionNotice(Contract contract)
    {
        var sender = I18nHelper.Get("mail.sender");
        var body = I18nHelper.Get("mail.needs_attention.body");

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue needs-attention notice contract={contract.Id.Value}.",
            LogLevel.Debug);
        if (!TrySendViaMfm(
            $"Dayswork.NeedsAttention.{CurrentDay()}.{Guid.NewGuid():N}",
            sender,
            body,
            new List<Item>(),
            0,
            DeliveryTiming.Today))
            ModEntry.ModMonitor.Log(
                "[Dayswork] Needs-attention notice could not be sent (Mail Framework Mod unavailable).",
                LogLevel.Warn);
    }

    public void QueueFestivalNotice(Contract contract, int refundGold)
    {
        var sender = I18nHelper.Get("mail.sender");
        var body   = refundGold > 0
            ? I18nHelper.Get("mail.festival.refund_body", new { refund = refundGold })
            : I18nHelper.Get("mail.festival.body");

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue festival notice refund={refundGold}.",
            LogLevel.Debug);
        if (!TrySendViaMfm(
            $"Dayswork.Festival.{CurrentDay()}.{Guid.NewGuid():N}",
            sender,
            body,
            new List<Item>(),
            refundGold,
            DeliveryTiming.Today))
        {
            // Fallback: credit the one-time refund directly so nothing is lost.
            if (refundGold > 0 && Game1.player is not null)
                Game1.player.Money += refundGold;
            ModEntry.ModMonitor.Log(
                "[Dayswork] Festival notice could not be sent (Mail Framework Mod unavailable).",
                LogLevel.Warn);
        }
    }

    private bool TrySendViaMfm(
        string id,
        string synopsis,
        string text,
        List<Item> attachments,
        int refundGold,
        DeliveryTiming timing)
    {
        if (_mfm is null) return false;
        try
        {
            var earliest = EarliestDeliveryDay(timing);
            ModEntry.ModMonitor.Log(
                $"[Dayswork][mail] register MFM letter id={id} attachments={attachments.Count} refund={refundGold} timing={timing}.",
                LogLevel.Debug);
            _mfm.RegisterLetter(id, synopsis, text, attachments, earliest, refundGold);
            if (timing == DeliveryTiming.Today)
                AddToMailboxToday(id);
            else
                PersistPendingSettlement(id, synopsis, text, attachments, earliest, refundGold);
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"[Dayswork] Mail Framework Mod letter registration failed: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private void PersistPendingSettlement(
        string id, string sender, string body,
        List<Item> attachments, int earliestDeliveryDay, int refundGold)
    {
        _pendingSettlements.RemoveAll(r => Game1.player?.mailReceived.Contains(r.Id) == true);
        _pendingSettlements.Add(new PendingSettlementRecord
        {
            Id = id,
            Sender = sender,
            Body = body,
            EarliestDeliveryDay = earliestDeliveryDay,
            RefundGold = refundGold,
            Items = attachments.Select(i => new PendingSettlementItemRecord
            {
                QualifiedItemId = i.QualifiedItemId,
                Quantity = i.Stack,
            }).ToList(),
        });
        _dataHelper.WriteSaveData(PendingSettlementsKey, _pendingSettlements);
    }

    private void ReRegisterWithMfm(PendingSettlementRecord record)
    {
        if (_mfm is null) return;
        try
        {
            var attachments = record.Items
                .Select(i => ItemRegistry.Create(i.QualifiedItemId, i.Quantity))
                .OfType<Item>()
                .ToList();

            ModEntry.ModMonitor.Log(
                $"[Dayswork][mail] re-register pending settlement id={record.Id} attachments={attachments.Count} refund={record.RefundGold}.",
                LogLevel.Debug);
            _mfm.RegisterLetter(
                record.Id, record.Sender, record.Body,
                attachments, record.EarliestDeliveryDay, record.RefundGold);
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Failed to re-register pending settlement with MFM: {ex.Message}",
                LogLevel.Warn);
        }
    }

    // Both Today and Tomorrow map to CurrentDay() because the date counter has already
    // advanced to the new day by the time GameLoop.Saving fires (SDV increments the date
    // before writing the save). A "+1" would overshoot by one and deliver mail a full extra
    // morning late. For Today letters the distinction from Tomorrow is handled by
    // AddToMailboxToday, not by the delivery-day value.
    private static int EarliestDeliveryDay(DeliveryTiming timing) => CurrentDay();

    // Morning skip notices are queued after the day's mailbox has usually been prepared. Adding the
    // MFM letter id to today's mailbox makes "no worker today" mail readable before the day is over.
    private static void AddToMailboxToday(string id)
    {
        if (Game1.mailbox is null || Game1.mailbox.Contains(id)) return;
        Game1.mailbox.Add(id);
    }

    private static List<Item> BuildItems(IReadOnlyList<ItemStack> stacks)
    {
        var result = new List<Item>(stacks.Count);
        foreach (var s in stacks)
        {
            var item = ItemRegistry.Create(s.QualifiedItemId, s.Quantity);
            if (item is not null)
                result.Add(item);
            else
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Could not create mail item '{s.QualifiedItemId}' x{s.Quantity}; skipped.", LogLevel.Warn);
        }
        return result;
    }

    // Settlement body: the overflow reason lines (when items are attached) plus a refund line (when
    // gold is returned). "^" is the vanilla letter line-break token, honored by MFM. (FD-Q6=A reused.)
    private static string BuildSettlementBody(IReadOnlyList<OverflowCategory> categories, int refundGold, bool hasItems)
    {
        var sb = new StringBuilder();
        if (hasItems)
            sb.Append(BuildOverflowBody(categories));

        if (refundGold > 0)
        {
            if (sb.Length > 0) sb.Append("^");
            sb.Append(I18nHelper.Get("mail.settlement.refund_line", new { refund = refundGold }));
        }
        return sb.ToString();
    }

    private static string BuildOverflowBody(IReadOnlyList<OverflowCategory> categories)
    {
        var sb = new StringBuilder();
        sb.Append(I18nHelper.Get("mail.overflow.intro"));
        foreach (var category in categories)
        {
            sb.Append("^");
            sb.Append(category.Reason switch
            {
                OverflowReason.ChestFull => I18nHelper.Get("mail.overflow.chest_full", new { scope = DescribeScope(category) }),
                OverflowReason.ChestMissing => I18nHelper.Get("mail.overflow.chest_missing", new { scope = DescribeScope(category) }),
                OverflowReason.ChestBusy => I18nHelper.Get("mail.overflow.chest_busy", new { scope = DescribeScope(category) }),
                OverflowReason.NoChestAssigned => I18nHelper.Get("mail.overflow.no_chest_assigned", new { scope = DescribeScope(category) }),
                OverflowReason.NotDelivered => I18nHelper.Get("mail.overflow.not_delivered", new { scope = DescribeScope(category) }),
                _ => string.Empty,
            });
        }
        return sb.ToString();
    }

    private static string DescribeScope(OverflowCategory category) =>
        category.ScopeFamily switch
        {
            OutputScopeFamily.Outdoor => I18nHelper.Get("mail.overflow.scope.outdoor"),
            OutputScopeFamily.Greenhouse => I18nHelper.Get("mail.overflow.scope.greenhouse"),
            OutputScopeFamily.AnimalBuilding when !string.IsNullOrWhiteSpace(category.ScopeName) =>
                I18nHelper.Get("mail.overflow.scope.animal_building_named", new { location = category.ScopeName }),
            OutputScopeFamily.AnimalBuilding => I18nHelper.Get("mail.overflow.scope.animal_buildings"),
            _ => I18nHelper.Get("mail.overflow.scope.general"),
        };

    private static int CurrentDay() => Game1.Date.TotalDays;
}
