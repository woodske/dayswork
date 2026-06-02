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
// Lists Active and Paused contracts with Pause/Resume/Cancel/Edit actions (S-12).
// All display strings are pre-computed in BuildRows(); draw() reads fields only (NFR-PERF-01).
internal sealed class ContractListMenu : IClickableMenu
{
    private const int MenuWidth    = 800;
    private const int BtnWidth     = 112; // text + 32px padding (16px each side for box border)
    private const int BtnHeight    = 48;
    private const int HeaderHeight = 70;
    private const int FooterHeight = 20;
    // Text area: full width minus 3-button strip (3×120px) + left/right margins
    private const int TextAreaWidth  = MenuWidth - (BtnWidth + 8) * 3 - 48;
    private const int RowPadTop      = 10;
    private const int RowPadBottom   = 8;
    private const int RowMetaHeight  = 32; // schedule + status line below task text
    private const int RowBtnHeight   = BtnHeight + 8;

    private readonly IContractStore _store;

    // Pre-computed per refresh; never touched in draw()
    private List<ContractRow> _rows = new();

    // i18n strings resolved once in ctor
    private readonly string _titleText;
    private readonly string _noContractsText;
    private readonly string _pauseLabel;
    private readonly string _resumeLabel;
    private readonly string _cancelLabel;
    private readonly string _editLabel;
    private readonly string _pausedLabel;
    private readonly string _activeLabel;
    private readonly string _oneTimeLabel;
    private readonly string _recurringLabel;
    private readonly string _cancelBlockedMsg;

    private sealed record ContractRow(
        Contract Contract,
        string WrappedTaskText,   // pre-wrapped to TextAreaWidth
        int    TextHeight,        // pixel height of WrappedTaskText
        string ScheduleLabel,
        string StatusLabel,
        int    RowY,              // top Y of this row on screen
        int    RowHeight,         // computed from text height
        ClickableComponent PauseResumeBtn,
        ClickableComponent CancelBtn,
        ClickableComponent EditBtn);

    internal ContractListMenu(IContractStore store, IModHelper helper)
        : base(0, 0, MenuWidth, HeaderHeight + FooterHeight) // height expanded in BuildRows
    {
        _store = store;

        _titleText       = I18nHelper.Get("ui.contract_list.title");
        _noContractsText = I18nHelper.Get("ui.contract_list.no_contracts");
        _pauseLabel      = I18nHelper.Get("ui.contract_list.pause");
        _resumeLabel     = I18nHelper.Get("ui.contract_list.resume");
        _cancelLabel     = I18nHelper.Get("ui.contract_list.cancel");
        _editLabel       = I18nHelper.Get("ui.contract_list.edit");
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
        var contracts = _store.List()
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Paused)
            .ToList();

        // First pass: compute per-row heights using a temporary Y to measure text.
        // We need xPositionOnScreen for button X, so anchor it before building rows.
        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, 400);
        xPositionOnScreen = (int)topLeft.X;

        int cursorY = 0; // relative offset from header bottom; resolved to screen Y in BuildRow
        var rows = new List<ContractRow>();
        foreach (var contract in contracts)
        {
            var row = BuildRow(contract, rows.Count, cursorY);
            cursorY += row.RowHeight;
            rows.Add(row);
        }

        int menuHeight = HeaderHeight + Math.Max(60, cursorY) + FooterHeight;
        height = menuHeight;

        // Re-anchor with final height
        topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        // Second pass: fix absolute screen Y now that yPositionOnScreen is settled.
        _rows = rows.Select((r, i) =>
        {
            int absRowY = yPositionOnScreen + HeaderHeight + r.RowY;
            int btnY    = absRowY + r.RowHeight - RowBtnHeight;
            int btnX    = xPositionOnScreen + MenuWidth - (BtnWidth + 8) * 3 - 16;
            int baseId  = 200 + i * 10;

            var pause = new ClickableComponent(
                new Rectangle(btnX, btnY, BtnWidth, BtnHeight),
                r.PauseResumeBtn.name, r.PauseResumeBtn.label)
            {
                myID            = r.PauseResumeBtn.myID,
                rightNeighborID = r.PauseResumeBtn.rightNeighborID,
                leftNeighborID  = r.PauseResumeBtn.leftNeighborID,
                upNeighborID    = r.PauseResumeBtn.upNeighborID,
                downNeighborID  = r.PauseResumeBtn.downNeighborID,
            };
            var cancel = new ClickableComponent(
                new Rectangle(btnX + BtnWidth + 8, btnY, BtnWidth, BtnHeight),
                r.CancelBtn.name, r.CancelBtn.label)
            {
                myID            = r.CancelBtn.myID,
                rightNeighborID = r.CancelBtn.rightNeighborID,
                leftNeighborID  = r.CancelBtn.leftNeighborID,
                upNeighborID    = r.CancelBtn.upNeighborID,
                downNeighborID  = r.CancelBtn.downNeighborID,
            };
            var edit = new ClickableComponent(
                new Rectangle(btnX + (BtnWidth + 8) * 2, btnY, BtnWidth, BtnHeight),
                r.EditBtn.name, r.EditBtn.label)
            {
                myID            = r.EditBtn.myID,
                rightNeighborID = r.EditBtn.rightNeighborID,
                leftNeighborID  = r.EditBtn.leftNeighborID,
                upNeighborID    = r.EditBtn.upNeighborID,
                downNeighborID  = r.EditBtn.downNeighborID,
            };
            return r with { RowY = absRowY, PauseResumeBtn = pause, CancelBtn = cancel, EditBtn = edit };
        }).ToList();

        populateClickableComponentList();
    }

    private ContractRow BuildRow(Contract contract, int index, int relativeRowY)
    {
        string rawTaskSummary = contract.EnabledTasks.Count > 0
            ? string.Join(", ", contract.EnabledTasks.Select(TaskLabel))
            : I18nHelper.Get("ui.common.none");

        // Wrap task text to the left text-area column (buttons occupy the right).
        string wrapped    = Game1.parseText(rawTaskSummary, Game1.smallFont, TextAreaWidth);
        int    textHeight = (int)Game1.smallFont.MeasureString(wrapped).Y;

        // Row height: padding + wrapped text + meta line + button strip
        int rowHeight = RowPadTop + textHeight + RowMetaHeight + RowBtnHeight + RowPadBottom;

        string scheduleLabel = contract.Schedule == ContractSchedule.Recurring
            ? _recurringLabel : _oneTimeLabel;

        string statusLabel = contract.Status == ContractStatus.Paused
            ? _pausedLabel : _activeLabel;

        string pauseResumeLabel = contract.Status == ContractStatus.Paused
            ? _resumeLabel : _pauseLabel;

        int baseId = 200 + index * 10;

        // Buttons positioned with placeholder bounds; Refresh() second pass corrects them.
        var pauseResumeBtn = new ClickableComponent(Rectangle.Empty, $"PauseResume_{index}", pauseResumeLabel)
        {
            myID            = baseId,
            rightNeighborID = baseId + 1,
            upNeighborID    = index > 0 ? baseId - 10 : -1,
            downNeighborID  = baseId + 10,
        };
        var cancelBtn = new ClickableComponent(Rectangle.Empty, $"Cancel_{index}", _cancelLabel)
        {
            myID            = baseId + 1,
            leftNeighborID  = baseId,
            rightNeighborID = baseId + 2,
            upNeighborID    = index > 0 ? baseId - 9 : -1,
            downNeighborID  = baseId + 11,
        };
        var editBtn = new ClickableComponent(Rectangle.Empty, $"Edit_{index}", _editLabel)
        {
            myID           = baseId + 2,
            leftNeighborID = baseId + 1,
            upNeighborID   = index > 0 ? baseId - 8 : -1,
            downNeighborID = baseId + 12,
        };

        return new ContractRow(contract, wrapped, textHeight, scheduleLabel, statusLabel,
            relativeRowY, rowHeight, pauseResumeBtn, cancelBtn, editBtn);
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var row in _rows)
        {
            if (row.PauseResumeBtn.bounds.Contains(x, y))
            {
                TogglePause(row.Contract);
                return;
            }
            if (row.CancelBtn.bounds.Contains(x, y))
            {
                TryCancel(row.Contract);
                return;
            }
            if (row.EditBtn.bounds.Contains(x, y))
            {
                exitThisMenu();
                ModEntry.Coordinator.OpenEditFlow(row.Contract.Id);
                return;
            }
        }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { exitThisMenu(); return; }
        base.receiveGamePadButton(b);
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
        if (ModEntry.Orchestrator.ActiveContractId == contract.Id)
        {
            Game1.addHUDMessage(new HUDMessage(_cancelBlockedMsg, HUDMessage.error_type));
            return;
        }
        _store.Cancel(contract.Id);
        Refresh();
    }

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        foreach (var row in _rows)
        {
            allClickableComponents.Add(row.PauseResumeBtn);
            allClickableComponents.Add(row.CancelBtn);
            allClickableComponents.Add(row.EditBtn);
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        if (_rows.Count > 0)
        {
            currentlySnappedComponent = _rows[0].PauseResumeBtn;
            snapCursorToCurrentSnappedComponent();
        }
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

        if (_rows.Count == 0)
        {
            Utility.drawTextWithShadow(
                b, _noContractsText, Game1.smallFont,
                new Vector2(xPositionOnScreen + 24, yPositionOnScreen + HeaderHeight + 24),
                Game1.textColor);
        }
        else
        {
            foreach (var row in _rows)
                DrawRow(b, row);
        }

        drawMouse(b);
    }

    private void DrawRow(SpriteBatch b, ContractRow row)
    {
        int rowY = row.RowY;

        // Row separator
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 8, rowY, MenuWidth - 16, 1),
            Color.LightGray * 0.5f);

        // Task summary + schedule + status
        Utility.drawTextWithShadow(b, row.WrappedTaskText, Game1.smallFont,
            new Vector2(xPositionOnScreen + 16, rowY + RowPadTop), Game1.textColor);

        Utility.drawTextWithShadow(b,
            $"{row.ScheduleLabel}  {row.StatusLabel}",
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 16, rowY + RowPadTop + row.TextHeight + 4),
            Color.DimGray);

        // Action buttons
        DrawSmallButton(b, row.PauseResumeBtn);
        DrawSmallButton(b, row.CancelBtn);
        DrawSmallButton(b, row.EditBtn);
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
