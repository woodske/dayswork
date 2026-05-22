using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Integration.MailFramework;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Integration;

// M-16 MailDispatcher (Pattern P). Sends the single overflow letter (with item attachments) and
// the no-item tool-missing warning, both for next-morning delivery. All user-visible text is
// routed through I18nHelper (UX-U14-01). No new Harmony patches (NFR-MAINT-04).
//
// DEVIATION (recorded): FD-Q7=A specified the tool-missing warning via vanilla addMailForTomorrow.
// It is sent through MFM here as a text-only (no-attachment) letter because vanilla custom mail
// cannot cleanly carry per-shift dynamic text (the skipped-task list) and re-deliver daily, while
// MFM (already a required dependency) handles text-only letters cleanly. FD-Q7=A's behavioural
// intent (one separate combined no-item warning per shift) is preserved.
internal sealed class MailDispatcher : IMailDispatcher
{
    private MailFrameworkModApiAdapter? _mfm;

    internal MailDispatcher(object? mfm = null)
    {
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

    public void QueueOverflowMail(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons)
    {
        if (items.Count == 0) return;

        var attachments = BuildItems(items);
        if (attachments.Count == 0) return;

        var sender = I18nHelper.Get("mail.sender");
        var body   = BuildOverflowBody(reasons);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue overflow letter attachments={attachments.Count} reasons={string.Join(",", reasons.OrderBy(r => r))}.",
            LogLevel.Debug);
        if (TrySendViaMfm($"Dayswork.Overflow.{CurrentDay()}.{Guid.NewGuid():N}", sender, body, attachments))
            return;

        // Last-resort safety net (SAFE-U14-01 / REL-U14-05): never lose items.
        ModEntry.ModMonitor.Log(
            "[Dayswork] Overflow mail could not be sent via Mail Framework Mod; depositing items in the shipping bin so nothing is lost.",
            LogLevel.Warn);
        var bin = Game1.getFarm().getShippingBin(Game1.player);
        foreach (var item in attachments)
            bin.Add(item);
    }

    public void QueueToolMissingWarning(IReadOnlySet<TaskKind> tasks)
    {
        if (tasks.Count == 0) return;

        var names  = string.Join(", ", tasks.OrderBy(t => (int)t).Select(TaskDisplayName));
        var sender = I18nHelper.Get("mail.sender");
        var body   = I18nHelper.Get("mail.warning.tool_missing", new { tasks = names });

        ModEntry.ModMonitor.Log(
            $"[Dayswork][mail] queue tool-warning letter tasks={names}.",
            LogLevel.Debug);
        if (!TrySendViaMfm($"Dayswork.ToolWarning.{CurrentDay()}.{Guid.NewGuid():N}", sender, body, new List<Item>()))
            ModEntry.ModMonitor.Log(
                "[Dayswork] Tool-missing warning mail could not be sent (Mail Framework Mod unavailable).",
                LogLevel.Warn);
    }

    private bool TrySendViaMfm(string id, string synopsis, string text, List<Item> attachments)
    {
        if (_mfm is null) return false;
        try
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][mail] register MFM letter id={id} attachments={attachments.Count}.",
                LogLevel.Debug);
            _mfm.RegisterLetter(id, synopsis, text, attachments, CurrentDay());
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"[Dayswork] Mail Framework Mod letter registration failed: {ex.Message}", LogLevel.Warn);
            return false;
        }
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

    // One line per distinct reason present, deterministic order (FD-Q6=A). "^" is the vanilla
    // letter line-break token, honored by MFM.
    private static string BuildOverflowBody(IReadOnlySet<OverflowReason> reasons)
    {
        var sb = new StringBuilder();
        sb.Append(I18nHelper.Get("mail.overflow.intro"));
        foreach (var reason in Enum.GetValues<OverflowReason>())
        {
            if (!reasons.Contains(reason)) continue;
            sb.Append("^");
            sb.Append(reason switch
            {
                OverflowReason.ChestFull       => I18nHelper.Get("mail.overflow.chest_full"),
                OverflowReason.ChestMissing    => I18nHelper.Get("mail.overflow.chest_missing"),
                OverflowReason.NoChestAssigned => I18nHelper.Get("mail.overflow.no_chest_assigned"),
                OverflowReason.NotDelivered    => I18nHelper.Get("mail.overflow.not_delivered"),
                _ => string.Empty,
            });
        }
        return sb.ToString();
    }

    private static int CurrentDay() => Game1.Date.TotalDays;

    private static string TaskDisplayName(TaskKind task) =>
        I18nHelper.Get("ui.task_selection." + task switch
        {
            TaskKind.WaterCrops            => "water_crops",
            TaskKind.HarvestCrops          => "harvest_crops",
            TaskKind.CollectFruit          => "collect_fruit",
            TaskKind.FeedAnimals           => "feed_animals",
            TaskKind.PetAnimals            => "pet_animals",
            TaskKind.CollectAnimalProducts => "collect_animal_products",
            TaskKind.CutTrees              => "cut_trees",
            TaskKind.ClearRocks            => "clear_rocks",
            TaskKind.ClearWeeds            => "clear_weeds",
            TaskKind.ClearGrass            => "clear_grass",
            _                              => "clear_weeds",
        });
}
