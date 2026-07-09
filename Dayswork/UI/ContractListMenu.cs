using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Contract management menu — opened from the hiring building's "Manage" action.
// Lists Active and Paused contracts with Pause/Resume/Cancel/Edit actions.
// All display strings are pre-computed in BuildRows(); draw() reads fields only.
internal sealed class ContractListMenu : IClickableMenu
{
    private const int MenuWidth    = 800;
    private const int BtnWidth     = 112; // text + 32px padding (16px each side for box border)
    private const int UpgradesBtnWidth = 140;
    private const int BtnHeight    = 48;
    private const int HeaderHeight = 70;
    private const int FooterHeight = 28;
    private const int RowPadTop      = 10;
    private const int RowPadBottom   = 8;
    private const int RowMetaHeight  = 32; // schedule + status line below task text
    private const int RowBtnHeight   = BtnHeight + 8;
    private const int BodySidePadding = 16;

    private readonly ContractStore _store;

    private List<ContractRowData> _allRows = new();
    private List<VisibleContractRow> _visibleRows = new();
    private Rectangle _bodyRect;
    private int _scrollIndex;
    private int _visibleRowCount;
    private int _maxScrollIndex;
    private bool _draggingScrollBar;
    private int _scrollDragOffset;

    // i18n strings resolved once in ctor
    private readonly string _titleText;
    private readonly string _noContractsText;
    private readonly string _pauseLabel;
    private readonly string _resumeLabel;
    private readonly string _cancelLabel;
    private readonly string _editLabel;
    private readonly string _upgradesLabel;
    private readonly string _hireLabel;
    private readonly string _pausedLabel;
    private readonly string _activeLabel;
    private readonly string _oneTimeLabel;
    private readonly string _recurringLabel;
    private readonly string _cancelBlockedMsg;

    private sealed record ContractRowData(
        Contract Contract,
        string  WrappedTaskText,   // pre-wrapped to TextAreaWidth
        int     TextHeight,        // pixel height of WrappedTaskText
        string? ManagedCropsLine,  // null → omit; non-null → render below task text
        int     CropLineHeight,    // 0 when ManagedCropsLine is null
        string  ScheduleLabel,
        string  StatusLabel,
        int     RowHeight);        // computed from text height

    private sealed record VisibleContractRow(
        ContractRowData Data,
        Rectangle RowBounds,
        ClickableComponent PauseResumeBtn,
        ClickableComponent CancelBtn,
        ClickableComponent EditBtn);

    private ClickableComponent? _upgradesBtn;
    private ClickableComponent? _hireBtn;   // shown only while under the contract capacity

    internal ContractListMenu(ContractStore store, IModHelper helper)
        : base(0, 0, MenuWidth, ContractMenuLayout.Height)
    {
        _store = store;

        _titleText       = I18nHelper.Get("ui.contract_list.title");
        _noContractsText = I18nHelper.Get("ui.contract_list.no_contracts");
        _pauseLabel      = I18nHelper.Get("ui.contract_list.pause");
        _resumeLabel     = I18nHelper.Get("ui.contract_list.resume");
        _cancelLabel     = I18nHelper.Get("ui.contract_list.cancel");
        _editLabel       = I18nHelper.Get("ui.contract_list.edit");
        _upgradesLabel   = I18nHelper.Get("ui.contract_list.upgrades");
        _hireLabel       = I18nHelper.Get("ui.contract_list.hire");
        _pausedLabel     = I18nHelper.Get("ui.contract_list.paused_label");
        _activeLabel     = I18nHelper.Get("ui.contract_list.active_label");
        _oneTimeLabel    = I18nHelper.Get("ui.contract_list.schedule_one_time");
        _recurringLabel  = I18nHelper.Get("ui.contract_list.schedule_recurring");
        _cancelBlockedMsg = I18nHelper.Get("ui.contract_list.cancel_blocked");

        Refresh();
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private void Refresh()
    {
        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        _bodyRect = new Rectangle(
            xPositionOnScreen + BodySidePadding,
            yPositionOnScreen + HeaderHeight,
            width - BodySidePadding * 2 - MenuScrollBar.ReservedWidth,
            height - HeaderHeight - FooterHeight);

        _upgradesBtn = new ClickableComponent(
            new Rectangle(
                xPositionOnScreen + width - UpgradesBtnWidth - 24,
                yPositionOnScreen + 16,
                UpgradesBtnWidth,
                BtnHeight),
            "Upgrades",
            _upgradesLabel);

        var contracts = _store.List()
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Paused)
            .ToList();

        // "Hire" appears left of Upgrades while there's still worker capacity.
        _hireBtn = contracts.Count < HiringFlowCoordinator.MaxActiveContracts
            ? new ClickableComponent(
                new Rectangle(
                    xPositionOnScreen + width - UpgradesBtnWidth - 24 - (BtnWidth + 12),
                    yPositionOnScreen + 16,
                    BtnWidth,
                    BtnHeight),
                "Hire",
                _hireLabel)
            : null;

        _allRows = contracts
            .Select((contract, index) => BuildRow(contract, index))
            .ToList();
        _maxScrollIndex = Math.Max(0, _allRows.Count - 1);
        _scrollIndex = Math.Clamp(_scrollIndex, 0, _maxScrollIndex);
        _visibleRowCount = ContractMenuViewport.GetVisibleVariableCount(
            _allRows.Select(row => row.RowHeight).ToList(),
            _scrollIndex,
            _bodyRect.Height);
        BuildVisibleRows();

        populateClickableComponentList();
    }

    private ContractRowData BuildRow(Contract contract, int index)
    {
        var textAreaWidth = _bodyRect.Width - (BtnWidth + 8) * 3 - 24;
        string taskList = contract.EnabledTasks.Count > 0
            ? string.Join(", ", contract.EnabledTasks.Select(TaskLabel))
            : I18nHelper.Get("ui.common.none");
        string rawTaskSummary =
            $"{Worker.FarmhandNpc.DisplayNameFor(contract.Preferences.WorkerName)} — {taskList}";

        // Wrap task text to the left text-area column (buttons occupy the right).
        string wrapped    = Game1.parseText(rawTaskSummary, Game1.smallFont, textAreaWidth);
        int    textHeight = (int)Game1.smallFont.MeasureString(wrapped).Y;

        string? managedCropsLine = null;
        int cropLineHeight = 0;
        if (contract.CropPlan.IsEnabled)
        {
            int groupCount = contract.CropPlan.Assignments
                .Select(a => a.GroupId ?? $"{a.Zone.LocationName}|{a.Mode}")
                .Distinct()
                .Count();
            managedCropsLine = I18nHelper.Get("ui.contract_list.managed_crops_groups",
                new { count = groupCount });
            cropLineHeight = (int)Game1.smallFont.MeasureString(managedCropsLine).Y + 4;
        }

        // Row height: padding + wrapped text + optional crop line + meta line + button strip
        int rowHeight = RowPadTop + textHeight + cropLineHeight + RowMetaHeight + RowBtnHeight + RowPadBottom;

        string scheduleLabel = contract.Schedule == ContractSchedule.Recurring
            ? _recurringLabel : _oneTimeLabel;

        string statusLabel = contract.Status == ContractStatus.Paused
            ? _pausedLabel : _activeLabel;
        return new ContractRowData(contract, wrapped, textHeight, managedCropsLine, cropLineHeight, scheduleLabel, statusLabel, rowHeight);
    }

    private void BuildVisibleRows()
    {
        _visibleRows.Clear();
        if (_allRows.Count == 0)
            return;

        var rowY = _bodyRect.Y;
        for (var i = _scrollIndex; i < _allRows.Count && rowY < _bodyRect.Bottom; i++)
        {
            var row = _allRows[i];
            var rowHeight = i == _scrollIndex
                ? Math.Min(row.RowHeight, _bodyRect.Height)
                : row.RowHeight;

            if (i > _scrollIndex && rowY + rowHeight > _bodyRect.Bottom)
                break;

            var rowBounds = new Rectangle(_bodyRect.X, rowY, _bodyRect.Width, rowHeight);
            var btnY = rowY + rowHeight - RowBtnHeight;
            var btnX = rowBounds.Right - (BtnWidth + 8) * 3;
            var baseId = 200 + i * 10;

            var pause = new ClickableComponent(
                new Rectangle(btnX, btnY, BtnWidth, BtnHeight),
                $"PauseResume_{i}",
                row.Contract.Status == ContractStatus.Paused ? _resumeLabel : _pauseLabel)
            {
                myID = baseId,
                rightNeighborID = baseId + 1,
                upNeighborID = i > 0 ? baseId - 10 : -1,
                downNeighborID = i < _allRows.Count - 1 ? baseId + 10 : -1,
            };

            var cancel = new ClickableComponent(
                new Rectangle(btnX + BtnWidth + 8, btnY, BtnWidth, BtnHeight),
                $"Cancel_{i}",
                _cancelLabel)
            {
                myID = baseId + 1,
                leftNeighborID = baseId,
                rightNeighborID = baseId + 2,
                upNeighborID = i > 0 ? baseId - 9 : -1,
                downNeighborID = i < _allRows.Count - 1 ? baseId + 11 : -1,
            };

            var edit = new ClickableComponent(
                new Rectangle(btnX + (BtnWidth + 8) * 2, btnY, BtnWidth, BtnHeight),
                $"Edit_{i}",
                _editLabel)
            {
                myID = baseId + 2,
                leftNeighborID = baseId + 1,
                upNeighborID = i > 0 ? baseId - 8 : -1,
                downNeighborID = i < _allRows.Count - 1 ? baseId + 12 : -1,
            };

            _visibleRows.Add(new VisibleContractRow(row, rowBounds, pause, cancel, edit));
            rowY += rowHeight;
        }
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (MenuScrollBar.TryBeginDrag(
                _bodyRect,
                _visibleRowCount,
                _allRows.Count,
                _scrollIndex,
                x,
                y,
                out _scrollDragOffset))
        {
            _draggingScrollBar = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_bodyRect, _allRows.Count, _visibleRowCount, x, y))
        {
            ScrollRows(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_bodyRect, _allRows.Count, _visibleRowCount, x, y))
        {
            ScrollRows(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_bodyRect, _visibleRowCount, _allRows.Count, x, y))
        {
            _scrollIndex = MenuScrollBar.GetTrackClickScrollIndex(
                _bodyRect,
                _visibleRowCount,
                _allRows.Count,
                _scrollIndex,
                y);
            Refresh();
            return;
        }

        foreach (var row in _visibleRows)
        {
            if (row.PauseResumeBtn.bounds.Contains(x, y))
            {
                TogglePause(row.Data.Contract);
                return;
            }
            if (row.CancelBtn.bounds.Contains(x, y))
            {
                TryCancel(row.Data.Contract);
                return;
            }
            if (row.EditBtn.bounds.Contains(x, y))
            {
                exitThisMenu();
                ModEntry.Coordinator.OpenEditFlow(row.Data.Contract.Id);
                return;
            }
        }

        if (_upgradesBtn?.bounds.Contains(x, y) == true)
        {
            Game1.playSound("smallSelect");
            ModEntry.Coordinator.ShowUpgradesFromManage();
            return;
        }

        if (_hireBtn?.bounds.Contains(x, y) == true)
        {
            Game1.playSound("smallSelect");
            exitThisMenu();
            ModEntry.Coordinator.OpenHiringFlow();
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_draggingScrollBar)
            return;

        var next = MenuScrollBar.GetDragScrollIndex(
            _bodyRect,
            _visibleRowCount,
            _allRows.Count,
            _scrollDragOffset,
            y);

        if (next == _scrollIndex)
            return;

        _scrollIndex = next;
        Refresh();
    }

    public override void releaseLeftClick(int x, int y)
    {
        _draggingScrollBar = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { exitThisMenu(); return; }
        base.receiveGamePadButton(b);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction != 0)
            ScrollRows(direction > 0 ? -1 : 1);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    private void TogglePause(Contract contract)
    {
        if (contract.Status == ContractStatus.Paused)
            _store.Resume(contract.Id);
        else
            _store.Pause(contract.Id);
        Refresh();
    }

    private void TryCancel(Contract contract)
    {
        if (ModEntry.Fleet.IsShiftRunning(contract.Id))
        {
            Game1.addHUDMessage(new HUDMessage(_cancelBlockedMsg, HUDMessage.error_type));
            return;
        }
        Game1.activeClickableMenu = new ConfirmCancelContractMenu(
            onGoBack:  () => Game1.activeClickableMenu = this,
            onConfirm: () => { _store.Cancel(contract.Id); Game1.activeClickableMenu = this; Refresh(); });
    }

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        if (_upgradesBtn is not null)
            allClickableComponents.Add(_upgradesBtn);
        if (_hireBtn is not null)
            allClickableComponents.Add(_hireBtn);
        foreach (var row in _visibleRows)
        {
            allClickableComponents.Add(row.PauseResumeBtn);
            allClickableComponents.Add(row.CancelBtn);
            allClickableComponents.Add(row.EditBtn);
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        if (_visibleRows.Count > 0)
        {
            currentlySnappedComponent = _visibleRows[0].PauseResumeBtn;
            snapCursorToCurrentSnappedComponent();
        }
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        if (id >= 200 && _allRows.Count > 0)
            EnsureRowVisible((id - 200) / 10);

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
        if (_hireBtn is not null)
            DrawSmallButton(b, _hireBtn);

        if (_allRows.Count == 0)
        {
            Utility.drawTextWithShadow(
                b, _noContractsText, Game1.smallFont,
                new Vector2(xPositionOnScreen + 24, yPositionOnScreen + HeaderHeight + 24),
                Game1.textColor);
        }
        else
        {
            foreach (var row in _visibleRows)
                DrawRow(b, row);
        }

        MenuScrollBar.Draw(b, _bodyRect, _visibleRowCount, _allRows.Count, _scrollIndex);

        drawMouse(b);
    }

    private void DrawRow(SpriteBatch b, VisibleContractRow row)
    {
        int rowY = row.RowBounds.Y;

        // Row separator
        b.Draw(Game1.staminaRect,
            new Rectangle(row.RowBounds.X, rowY, row.RowBounds.Width, 1),
            Color.LightGray * 0.5f);

        // Task summary + optional managed-crops line + schedule + status
        Utility.drawTextWithShadow(b, row.Data.WrappedTaskText, Game1.smallFont,
            new Vector2(row.RowBounds.X + 8, rowY + RowPadTop), Game1.textColor);

        if (row.Data.ManagedCropsLine != null)
        {
            Utility.drawTextWithShadow(b, row.Data.ManagedCropsLine, Game1.smallFont,
                new Vector2(row.RowBounds.X + 8, rowY + RowPadTop + row.Data.TextHeight + 4),
                Color.DimGray);
        }

        Utility.drawTextWithShadow(b,
            $"{row.Data.ScheduleLabel}  {row.Data.StatusLabel}",
            Game1.smallFont,
            new Vector2(row.RowBounds.X + 8, rowY + RowPadTop + row.Data.TextHeight + row.Data.CropLineHeight + 4),
            Color.DimGray);

        // Action buttons
        DrawSmallButton(b, row.PauseResumeBtn);
        DrawSmallButton(b, row.CancelBtn);
        DrawSmallButton(b, row.EditBtn);
    }

    private void ScrollRows(int delta)
    {
        var next = Math.Clamp(_scrollIndex + delta, 0, _maxScrollIndex);
        if (next != _scrollIndex)
        {
            _scrollIndex = next;
            Refresh();
        }
    }

    private void EnsureRowVisible(int rowIndex)
    {
        if (rowIndex < _scrollIndex)
        {
            _scrollIndex = rowIndex;
            Refresh();
            return;
        }

        var lastVisibleIndex = _scrollIndex + Math.Max(0, _visibleRowCount - 1);
        if (rowIndex <= lastVisibleIndex)
            return;

        _scrollIndex = Math.Clamp(rowIndex, 0, _maxScrollIndex);
        Refresh();
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
