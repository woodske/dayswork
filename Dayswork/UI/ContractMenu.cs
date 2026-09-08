using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Contract management menu — opened from an office's tile action. One contract per office (hard
// rule 3), shown with the actions the player at this screen is entitled to: everything for the
// owner, Pause/Cancel (and Claim on an orphaned office) for the host, read-only for anyone else
// (2.0 plan D4). Every action goes out as a request, which on the host is answered by a direct
// call — so single-player, host, split-screen guest, and remote client share one path.
// All display strings are pre-computed in Refresh(); draw() reads fields only.
internal sealed class ContractMenu : IClickableMenu
{
    private const int MenuWidth    = 800;
    private const int BtnWidth     = 112; // text + 32px padding (16px each side for box border)
    private const int UpgradesBtnWidth = 140;
    private const int BtnHeight    = 48;
    private const int HeaderHeight = 70;
    private const int FooterHeight = 28;
    private const int BodyPadTop      = 10;
    private const int BodySidePadding = 16;
    private const int MetaGap      = 12; // gap between the meta line and the button strip
    private const float NameScale = 1.15f; // worker name draws slightly larger than body text

    private static readonly Color NameColor = new(80, 60, 40);
    private static readonly Color ActiveStatusColor = Color.DarkGreen;
    private static readonly Color PausedStatusColor = Color.Gray;

    private readonly OfficeContractStore _store;
    private readonly Guid _officeId;
    // Recomputed on every Refresh rather than pinned in the constructor: claiming an orphaned
    // office makes the viewer its owner, so the page has to redraw with the owner's actions.
    private OfficeViewerRole _role;
    private string _ownerLine = "";

    private ContractView? _view;
    private Rectangle _bodyRect;
    private ClickableComponent? _claimBtn;

    // i18n strings resolved once in ctor
    private readonly string _titleText;
    private readonly string _noContractText;
    private readonly string _pauseLabel;
    private readonly string _resumeLabel;
    private readonly string _cancelLabel;
    private readonly string _editLabel;
    private readonly string _upgradesLabel;
    private readonly string _pausedLabel;
    private readonly string _activeLabel;
    private readonly string _oneTimeLabel;
    private readonly string _recurringLabel;
    private readonly string _cancelBlockedMsg;
    private readonly string _claimLabel;

    private sealed record ContractView(
        Contract Contract,
        string  WrappedNameText,      // worker name, pre-wrapped for NameScale
        int     NameHeight,           // pixel height of WrappedNameText at NameScale
        string  WrappedTaskText,      // pre-wrapped to the text-area width
        int     TextHeight,           // pixel height of WrappedTaskText
        IReadOnlyList<string> InfoLines, // pre-wrapped managed-crops/machines/fish-pond summary lines
        string  ScheduleLabel,
        string  TierLabel,
        string  StatusLabel,
        Color   StatusColor,
        ClickableComponent? PauseResumeBtn,
        ClickableComponent? CancelBtn,
        ClickableComponent? EditBtn);

    private ClickableComponent? _upgradesBtn;

    internal ContractMenu(OfficeContractStore store, Guid officeId)
        : base(0, 0, MenuWidth, ContractMenuLayout.Height)
    {
        _store = store;
        _officeId = officeId;

        _titleText       = I18nHelper.Get("ui.contract.title");
        _noContractText  = I18nHelper.Get("ui.contract.no_contract");
        _pauseLabel      = I18nHelper.Get("ui.contract.pause");
        _resumeLabel     = I18nHelper.Get("ui.contract.resume");
        _cancelLabel     = I18nHelper.Get("ui.contract.cancel");
        _editLabel       = I18nHelper.Get("ui.contract.edit");
        _upgradesLabel   = I18nHelper.Get("ui.contract.upgrades");
        _pausedLabel     = I18nHelper.Get("ui.contract.paused_label");
        _activeLabel     = I18nHelper.Get("ui.contract.active_label");
        _oneTimeLabel    = I18nHelper.Get("ui.contract.schedule_one_time");
        _recurringLabel  = I18nHelper.Get("ui.contract.schedule_recurring");
        _cancelBlockedMsg = I18nHelper.Get("ui.contract.cancel_blocked");
        _claimLabel      = I18nHelper.Get("ui.contract.claim");

        Refresh();
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private void Refresh()
    {
        var office = OfficeResolver.TryGet(_officeId);
        _role = OfficeViewerRoles.Resolve(office);
        _ownerLine = I18nHelper.Get("ui.contract.owner", new
        {
            player = OfficeViewerRoles.OwnerName(office is null ? 0L : Net.ContractRequestHandler.ResolveOwner(office)),
        });

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        _bodyRect = new Rectangle(
            xPositionOnScreen + BodySidePadding,
            yPositionOnScreen + HeaderHeight,
            width - BodySidePadding * 2,
            height - HeaderHeight - FooterHeight);

        // Upgrades are per player and bought from your own wallet, so the page is only offered to
        // the owner — buying from someone else's office would charge the wrong person.
        _upgradesBtn = _role.CanEdit()
            ? new ClickableComponent(
                new Rectangle(
                    xPositionOnScreen + width - UpgradesBtnWidth - 24,
                    yPositionOnScreen + 16,
                    UpgradesBtnWidth,
                    BtnHeight),
                "Upgrades",
                _upgradesLabel)
            {
                myID = 100,
                downNeighborID = 200,
            }
            : null;

        _claimBtn = _role.CanClaim()
            ? new ClickableComponent(
                new Rectangle(
                    xPositionOnScreen + width - UpgradesBtnWidth - 24,
                    yPositionOnScreen + 16,
                    UpgradesBtnWidth,
                    BtnHeight),
                "Claim",
                _claimLabel)
            {
                myID = 101,
                downNeighborID = 200,
            }
            : null;

        // This office's contract, not "the farm's" — each office has its own page. A contract
        // that has run its course (Executed) or been cancelled reads as "no contract", exactly as
        // it did in 1.x, so the page offers hiring again.
        var contract = _store.ForOffice(_officeId);
        _view = contract is { Status: ContractStatus.Active or ContractStatus.Paused }
            ? BuildView(contract)
            : null;

        populateClickableComponentList();
    }

    private ContractView BuildView(Contract contract)
    {
        var textAreaWidth = _bodyRect.Width - 16;

        // Worker name gets its own line, drawn at NameScale — the page's visual header.
        string rawName = Worker.FarmhandNpc.DisplayNameFor(contract.Preferences.WorkerName);
        string wrappedName = Game1.parseText(rawName, Game1.smallFont, (int)(textAreaWidth / NameScale));
        int nameHeight = (int)(Game1.smallFont.MeasureString(wrappedName).Y * NameScale) + 4;

        string taskList = contract.EnabledTasks.Count > 0
            ? string.Join(", ", contract.EnabledTasks.Select(TaskLabel))
            : I18nHelper.Get("ui.common.none");

        string wrapped    = Game1.parseText(taskList, Game1.smallFont, textAreaWidth);
        int    textHeight = (int)Game1.smallFont.MeasureString(wrapped).Y;

        var infoLines = new List<string>();

        if (contract.CropPlan.IsEnabled)
        {
            int groupCount = contract.CropPlan.Assignments
                .Select(a => a.GroupId ?? $"{a.Zone.LocationName}|{a.Mode}")
                .Distinct()
                .Count();
            infoLines.Add(I18nHelper.Get("ui.contract.managed_crops_groups",
                new { count = groupCount }));
        }

        if (contract.MachineScope.IsEnabled)
        {
            string groups = string.Join(", ", contract.MachineScope.Groups
                .Select(group => $"{MachineTypeDisplayName(group.MachineType)} ×{group.Machines.Count}"));
            infoLines.Add(I18nHelper.Get("ui.contract.managed_machines_groups", new { groups }));
        }

        if (contract.FishPondScope.IsEnabled)
        {
            infoLines.Add(I18nHelper.Get("ui.contract.managed_fish_ponds",
                new { count = contract.FishPondScope.Ponds.Count }));
        }

        var wrappedInfoLines = new List<string>(infoLines.Count);
        int infoLinesHeight = 0;
        foreach (var line in infoLines)
        {
            string wrappedLine = Game1.parseText(line, Game1.smallFont, textAreaWidth);
            wrappedInfoLines.Add(wrappedLine);
            infoLinesHeight += (int)Game1.smallFont.MeasureString(wrappedLine).Y + 4;
        }

        string scheduleLabel = contract.Schedule == ContractSchedule.Recurring
            ? _recurringLabel : _oneTimeLabel;

        string tierLabel = I18nHelper.Get($"ui.summary.tier.{TierKey(contract.Tier)}");

        bool isPaused = contract.Status == ContractStatus.Paused;
        string statusLabel = isPaused ? _pausedLabel : _activeLabel;
        Color statusColor = isPaused ? PausedStatusColor : ActiveStatusColor;

        // Buttons sit just below the content block, with the owner line between; a single
        // contract's summary always fits the fixed page height, so there is nothing to scroll.
        // Clamped to the body bottom regardless.
        int metaHeight = ((int)Game1.smallFont.MeasureString(scheduleLabel).Y + 4) * 2;
        int btnY = Math.Min(
            _bodyRect.Y + BodyPadTop + nameHeight + textHeight + infoLinesHeight + metaHeight + MetaGap,
            _bodyRect.Bottom - BtnHeight);
        int btnX = _bodyRect.Right - (BtnWidth + 8) * 3;

        // Pause is offered to anyone who may administer the contract; Resume only to its owner —
        // the host may stop an absent player's worker, but restarting it is the owner's call.
        var showPauseResume = isPaused ? _role.CanResume() : _role.CanPauseOrCancel();
        var pause = showPauseResume
            ? new ClickableComponent(
                new Rectangle(btnX, btnY, BtnWidth, BtnHeight),
                "PauseResume",
                isPaused ? _resumeLabel : _pauseLabel)
            {
                myID = 200,
                rightNeighborID = 201,
                upNeighborID = 100,
            }
            : null;

        var cancel = _role.CanPauseOrCancel()
            ? new ClickableComponent(
                new Rectangle(btnX + BtnWidth + 8, btnY, BtnWidth, BtnHeight),
                "Cancel",
                _cancelLabel)
            {
                myID = 201,
                leftNeighborID = 200,
                rightNeighborID = 202,
                upNeighborID = 100,
            }
            : null;

        var edit = _role.CanEdit()
            ? new ClickableComponent(
                new Rectangle(btnX + (BtnWidth + 8) * 2, btnY, BtnWidth, BtnHeight),
                "Edit",
                _editLabel)
            {
                myID = 202,
                leftNeighborID = 201,
                upNeighborID = 100,
            }
            : null;

        return new ContractView(
            contract, wrappedName, nameHeight, wrapped, textHeight, wrappedInfoLines,
            scheduleLabel, tierLabel, statusLabel, statusColor, pause, cancel, edit);
    }

    private static string MachineTypeDisplayName(string? machineType) =>
        machineType is not null
            ? ItemRegistry.GetData(machineType)?.DisplayName ?? machineType
            : "?";

    private static string TierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
        _ => tier.ToString(),
    };

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_view is not null)
        {
            if (_view.PauseResumeBtn?.bounds.Contains(x, y) == true)
            {
                TogglePause(_view.Contract);
                return;
            }
            if (_view.CancelBtn?.bounds.Contains(x, y) == true)
            {
                TryCancel(_view.Contract);
                return;
            }
            if (_view.EditBtn?.bounds.Contains(x, y) == true)
            {
                exitThisMenu();
                ModEntry.Coordinator.OpenEditFlow(_officeId);
                return;
            }
        }

        if (_claimBtn?.bounds.Contains(x, y) == true)
        {
            Game1.playSound("smallSelect");
            Submit(ContractActionKind.ClaimOffice);
            return;
        }

        if (_upgradesBtn?.bounds.Contains(x, y) == true)
        {
            Game1.playSound("smallSelect");
            ModEntry.Coordinator.ShowUpgradesFromManage(_officeId);
        }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { exitThisMenu(); return; }
        base.receiveGamePadButton(b);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    private void TogglePause(Contract contract) =>
        Submit(contract.Status == ContractStatus.Paused ? ContractActionKind.Resume : ContractActionKind.Pause);

    private void TryCancel(Contract contract)
    {
        // A running shift is stopped by the day, not by the menu: the worker is out with items on
        // it. The host validates this too; catching it here keeps the confirmation dialog from
        // opening on something that cannot succeed.
        if (ModEntry.Fleet.IsShiftRunning(contract.Id))
        {
            Game1.addHUDMessage(new HUDMessage(_cancelBlockedMsg, HUDMessage.error_type));
            return;
        }

        Game1.activeClickableMenu = new ConfirmCancelContractMenu(
            onGoBack:  () => Game1.activeClickableMenu = this,
            onConfirm: () => { Game1.activeClickableMenu = this; Submit(ContractActionKind.Cancel); });
    }

    /// <summary>
    /// Sends one contract action and refreshes when the answer comes back. On the host that is the
    /// same tick; on a remote client it is a round trip, so the menu may have been closed by then —
    /// the refresh checks before touching itself.
    /// </summary>
    private void Submit(ContractActionKind action)
    {
        ModEntry.Requests.SubmitAction(_officeId, action, response =>
        {
            if (!response.Accepted)
            {
                Game1.addHUDMessage(new HUDMessage(
                    ContractRejectionText.Describe(response.Code),
                    HUDMessage.error_type));
            }

            if (ReferenceEquals(Game1.activeClickableMenu, this))
                Refresh();
        });
    }

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        if (_upgradesBtn is not null)
            allClickableComponents.Add(_upgradesBtn);
        if (_claimBtn is not null)
            allClickableComponents.Add(_claimBtn);
        if (_view is not null)
        {
            if (_view.PauseResumeBtn is not null)
                allClickableComponents.Add(_view.PauseResumeBtn);
            if (_view.CancelBtn is not null)
                allClickableComponents.Add(_view.CancelBtn);
            if (_view.EditBtn is not null)
                allClickableComponents.Add(_view.EditBtn);
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _view?.PauseResumeBtn ?? _upgradesBtn ?? _claimBtn;
        if (currentlySnappedComponent is not null)
            snapCursorToCurrentSnappedComponent();
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b, _titleText, Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 24, yPositionOnScreen + 16),
            Game1.textColor);

        if (_upgradesBtn is not null)
            DrawSmallButton(b, _upgradesBtn);
        if (_claimBtn is not null)
            DrawSmallButton(b, _claimBtn);

        if (_view is null)
        {
            Utility.drawTextWithShadow(
                b, _noContractText, Game1.smallFont,
                new Vector2(xPositionOnScreen + 24, yPositionOnScreen + HeaderHeight + 24),
                Game1.textColor);
            // Whose empty office this is, so a visitor knows why they cannot hire from it.
            Utility.drawTextWithShadow(
                b, _ownerLine, Game1.smallFont,
                new Vector2(xPositionOnScreen + 24, yPositionOnScreen + HeaderHeight + 24 + Game1.smallFont.MeasureString(_noContractText).Y + 4),
                Color.DimGray);
        }
        else
        {
            DrawContract(b, _view);
        }

        drawMouse(b);
    }

    private void DrawContract(SpriteBatch b, ContractView view)
    {
        // Worker name (header line) + task summary + optional info lines + schedule/tier/status
        Utility.drawTextWithShadow(b, view.WrappedNameText, Game1.smallFont,
            new Vector2(_bodyRect.X + 8, _bodyRect.Y + BodyPadTop), NameColor, NameScale);

        var taskY = _bodyRect.Y + BodyPadTop + view.NameHeight;
        Utility.drawTextWithShadow(b, view.WrappedTaskText, Game1.smallFont,
            new Vector2(_bodyRect.X + 8, taskY), Game1.textColor);

        var infoLineY = taskY + view.TextHeight + 4;
        foreach (var line in view.InfoLines)
        {
            Utility.drawTextWithShadow(b, line, Game1.smallFont,
                new Vector2(_bodyRect.X + 8, infoLineY), Color.DimGray);
            infoLineY += (int)Game1.smallFont.MeasureString(line).Y + 4;
        }

        var metaPos = new Vector2(_bodyRect.X + 8, infoLineY);
        string metaPrefix = $"{view.ScheduleLabel}  {view.TierLabel}  ";
        Utility.drawTextWithShadow(b, metaPrefix, Game1.smallFont, metaPos, Color.DimGray);
        var statusX = metaPos.X + Game1.smallFont.MeasureString(metaPrefix).X;
        Utility.drawTextWithShadow(b, view.StatusLabel, Game1.smallFont,
            new Vector2(statusX, metaPos.Y), view.StatusColor);

        // Owner line — who is paying for this farmhand. Always drawn: on a co-op farm with several
        // offices it is the fastest way to tell whose card you are looking at.
        Utility.drawTextWithShadow(b, _ownerLine, Game1.smallFont,
            new Vector2(metaPos.X, metaPos.Y + Game1.smallFont.MeasureString(metaPrefix).Y + 2), Color.DimGray);

        // Action buttons — only the ones this player may use.
        if (view.PauseResumeBtn is not null)
            DrawSmallButton(b, view.PauseResumeBtn);
        if (view.CancelBtn is not null)
            DrawSmallButton(b, view.CancelBtn);
        if (view.EditBtn is not null)
            DrawSmallButton(b, view.EditBtn);
    }

    private static void DrawSmallButton(SpriteBatch b, ClickableComponent btn)
    {
        IClickableMenu.drawTextureBox(b,
            btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);
        var size = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(b, btn.label, Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width  - (int)size.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)size.Y) / 2),
            Game1.textColor);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string TaskLabel(TaskKind task) => task switch
    {
        TaskKind.WaterCrops            => I18nHelper.Get("ui.task_selection.water_crops"),
        TaskKind.HarvestCrops          => I18nHelper.Get("ui.task_selection.harvest_crops"),
        TaskKind.CollectFruit          => I18nHelper.Get("ui.task_selection.collect_fruit"),
        TaskKind.FeedAnimals           => I18nHelper.Get("ui.task_selection.feed_animals"),
        TaskKind.PetAnimals            => I18nHelper.Get("ui.task_selection.pet_animals"),
        TaskKind.CollectAnimalProducts => I18nHelper.Get("ui.task_selection.collect_animal_products"),
        TaskKind.CutTrees              => I18nHelper.Get("ui.task_selection.cut_trees"),
        TaskKind.ClearRocks            => I18nHelper.Get("ui.task_selection.clear_rocks"),
        TaskKind.ClearWeeds            => I18nHelper.Get("ui.task_selection.clear_weeds"),
        TaskKind.ClearGrass            => I18nHelper.Get("ui.task_selection.clear_grass"),
        _                              => task.ToString(),
    };
}
