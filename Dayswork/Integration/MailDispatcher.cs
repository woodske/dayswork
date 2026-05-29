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

// M-16 MailDispatcher (Patterns P + U). Sends the single per-shift settlement letter (overflow
// items only — U-21 BR-END-03 removed shift-end refund settlement), plus the text-only
// cannot-afford / needs-attention / festival notices. All user-visible text is routed through
// I18nHelper (UX-U15-01). No new Harmony patches (NFR-MAINT-04).
//
// One-time contracts cancelled by a festival still issue a refund via QueueFestivalNotice
// (BR-CAL-03); recurring contracts on festival days are not charged at all (no refund needed).
// Refund gold rides the festival letter as a credit-on-collection callback (DEV-U15-04). If the
// mail backend is unavailable, items fall back to the shipping bin and gold is credited directly
// so nothing is lost (SAFE-U15-01 / REL-U15-04).
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

    public void QueueSettlement(IReadOnlyList<ItemStack> items, IReadOnlyList<OverflowCategory> categories)
    {
        if (items.Count == 0) return;

        var attachments = BuildItems(items);
        if (attachments.Count == 0)
        {
            ModEntry.ModMonitor.Log(
                "[Dayswork] Settlement mail had no valid item attachments; suppressing empty letter.",
                LogLevel.Warn);
            return;
        }

        var sender = I18nHelper.Get("mail.sender");
        var body   = BuildSettlementBody(categories);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue settlement letter attachments={attachments.Count} categories={string.Join(",", categories.Select(category => $"{category.Reason}:{category.ScopeFamily}:{category.ScopeName}"))}.",
            LogLevel.Trace);
        if (TrySendViaMfm(
            $"Dayswork.Settlement.{CurrentDay()}.{Guid.NewGuid():N}",
            sender,
            body,
            attachments,
            refundGold: 0,
            DeliveryTiming.Tomorrow))
            return;

        // Last-resort safety net (SAFE-U15-01 / REL-U15-04): never lose items.
        ModEntry.ModMonitor.Log(
            "[Dayswork] Settlement mail could not be sent via Mail Framework Mod; depositing items in the shipping bin so nothing is lost.",
            LogLevel.Warn);
        var bin = Game1.getFarm().getShippingBin(Game1.player);
        foreach (var item in attachments)
            bin.Add(item);
    }

    public void QueueCannotAffordNotice(Contract contract, int dailyPrice, int shortfall)
    {
        var sender = I18nHelper.Get("mail.sender");
        var body   = I18nHelper.Get("mail.cannot_afford.body", new { price = dailyPrice, shortfall });

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue cannot-afford notice price={dailyPrice} shortfall={shortfall}.",
            LogLevel.Trace);
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
            LogLevel.Trace);
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
            LogLevel.Trace);
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
                LogLevel.Trace);
            _mfm.RegisterLetter(id, synopsis, text, attachments, earliest, refundGold);
            if (timing == DeliveryTiming.Today)
                AddToMailboxToday(id);
            else
                PersistPendingSettlement(id, synopsis, text, attachments, earliest);
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
        List<Item> attachments, int earliestDeliveryDay)
    {
        _pendingSettlements.RemoveAll(r => Game1.player?.mailReceived.Contains(r.Id) == true);

        // Filter out anything that would round-trip as an Error Item next save load
        // (empty/whitespace QualifiedItemId or already-an-Error-Item). Log loudly so we can
        // identify the writer that fed us the bad attachment.
        var records = new List<PendingSettlementItemRecord>(attachments.Count);
        foreach (var i in attachments)
        {
            if (string.IsNullOrWhiteSpace(i.QualifiedItemId) || IsErrorItem(i))
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][mail] Error Item suppressed at PersistPendingSettlement: letterId={id} qualifiedId='{i.QualifiedItemId}' name='{i.Name}' stack=x{i.Stack}; not persisted.",
                    LogLevel.Error);
                continue;
            }
            records.Add(new PendingSettlementItemRecord
            {
                QualifiedItemId = i.QualifiedItemId,
                Quantity = i.Stack,
            });
        }

        _pendingSettlements.Add(new PendingSettlementRecord
        {
            Id = id,
            Sender = sender,
            Body = body,
            EarliestDeliveryDay = earliestDeliveryDay,
            Items = records,
        });
        _dataHelper.WriteSaveData(PendingSettlementsKey, _pendingSettlements);
    }

    private void ReRegisterWithMfm(PendingSettlementRecord record)
    {
        if (_mfm is null) return;
        try
        {
            var attachments = new List<Item>(record.Items.Count);
            var suppressed = 0;
            foreach (var rec in record.Items)
            {
                if (string.IsNullOrWhiteSpace(rec.QualifiedItemId))
                {
                    suppressed++;
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork][mail] Error Item suppressed at ReRegisterWithMfm: empty QualifiedItemId letterId={record.Id} stack=x{rec.Quantity}; skipped.",
                        LogLevel.Error);
                    continue;
                }

                var item = ItemRegistry.Create(rec.QualifiedItemId, rec.Quantity);
                if (item is null)
                {
                    suppressed++;
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork][mail] ReRegisterWithMfm: ItemRegistry.Create returned null for '{rec.QualifiedItemId}' x{rec.Quantity} letterId={record.Id}; skipped.",
                        LogLevel.Warn);
                    continue;
                }
                if (IsErrorItem(item))
                {
                    suppressed++;
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork][mail] Error Item suppressed at ReRegisterWithMfm: rawId='{rec.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' letterId={record.Id} stack=x{rec.Quantity}; skipped.",
                        LogLevel.Error);
                    continue;
                }
                attachments.Add(item);
            }

            // Only the settlement (overflow-items) path persists; the festival refund letter
            // uses DeliveryTiming.Today and is never persisted. So a pending record never carries
            // a refund — if every attachment was suppressed, the letter has nothing to deliver.
            if (attachments.Count == 0)
            {
                _pendingSettlements.Remove(record);
                _dataHelper.WriteSaveData(PendingSettlementsKey, _pendingSettlements);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][mail] dropped pending settlement id={record.Id}: all {suppressed} attachment(s) were invalid.",
                    LogLevel.Warn);
                return;
            }

            ModEntry.ModMonitor.Log(
                $"[Dayswork][mail] re-register pending settlement id={record.Id} attachments={attachments.Count} suppressed={suppressed}.",
                suppressed > 0 ? LogLevel.Warn : LogLevel.Trace);
            _mfm.RegisterLetter(
                record.Id, record.Sender, record.Body,
                attachments, record.EarliestDeliveryDay, moneyReward: 0);
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
            if (string.IsNullOrWhiteSpace(s.QualifiedItemId))
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][mail] Error Item suppressed at BuildItems: empty QualifiedItemId stack=x{s.Quantity}; skipped.",
                    LogLevel.Error);
                continue;
            }

            var item = ItemRegistry.Create(s.QualifiedItemId, s.Quantity);
            if (item is null)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Could not create mail item '{s.QualifiedItemId}' x{s.Quantity}; skipped.",
                    LogLevel.Warn);
                continue;
            }
            if (IsErrorItem(item))
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][mail] Error Item suppressed at BuildItems: rawId='{s.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' stack=x{s.Quantity}; skipped.",
                    LogLevel.Error);
                continue;
            }
            result.Add(item);
        }
        return result;
    }

    // ItemRegistry.Create(badId) returns SDV's fallback Error Item (Name="Error Item",
    // QualifiedItemId="(O)") rather than null when the id can't be resolved. .OfType<Item>()
    // and `is not null` checks don't catch it — this helper does.
    private static bool IsErrorItem(Item item) =>
        item is null
        || string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);

    // Settlement body: the overflow reason lines for the attached items. "^" is the vanilla letter
    // line-break token, honored by MFM. (FD-Q6=A reused.) U-21 BR-END-03 removed the refund line.
    private static string BuildSettlementBody(IReadOnlyList<OverflowCategory> categories) =>
        BuildOverflowBody(categories);

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
