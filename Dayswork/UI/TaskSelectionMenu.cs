using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class TaskSelectionMenu : IClickableMenu
{
    private const int MenuWidth = 700;
    private const int MenuHeight = 700;
    private const int RowHeight = 58;

    private readonly ContractDraft _draft;
    private readonly Action<TaskKind> _onToggleTask;
    private readonly Action<ContractDraft> _onAdvance;
    private readonly Action _onCancel;

    private readonly List<ClickableComponent> _toggleRows = new();
    private Rectangle _taskListRect;
    private int _taskScrollIndex;
    private int _maxTaskScrollIndex;
    private bool _draggingTaskScrollBar;
    private int _taskScrollDragOffset;
    private string _hoverText = string.Empty;

    private ClickableComponent _nextBtn = null!;
    private ClickableComponent _cancelBtn = null!;

    public TaskSelectionMenu(
        ContractDraft draft,
        Action<TaskKind> onToggleTask,
        Action<ContractDraft> onAdvance,
        Action onCancel)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft = draft;
        _onToggleTask = onToggleTask;
        _onAdvance = onAdvance;
        _onCancel = onCancel;

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        _toggleRows.Clear();

        var rowX = xPositionOnScreen + 48;
        var rowW = MenuWidth - 96 - MenuScrollBar.ReservedWidth;
        var startY = yPositionOnScreen + 75;
        var btnY = yPositionOnScreen + MenuHeight - 70;
        _taskListRect = new Rectangle(rowX, startY, rowW, btnY - startY - 20);
        _maxTaskScrollIndex = Math.Max(0, TaskPresentation.TaskOrder.Length - GetVisibleTaskRowCount());
        _taskScrollIndex = Math.Clamp(_taskScrollIndex, 0, _maxTaskScrollIndex);
        startY -= _taskScrollIndex * RowHeight;

        for (var i = 0; i < TaskPresentation.TaskOrder.Length; i++)
        {
            var task = TaskPresentation.TaskOrder[i];
            _toggleRows.Add(new ClickableComponent(
                new Rectangle(rowX, startY + i * RowHeight, rowW, RowHeight - 4),
                name: task.ToString(),
                label: I18nHelper.Get(TaskPresentation.GetI18nKey(task)))
            {
                myID = 100 + i,
                upNeighborID = i > 0 ? 99 + i : -1,
                downNeighborID = i < TaskPresentation.TaskOrder.Length - 1 ? 101 + i : 200,
            });
        }

        _nextBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next", I18nHelper.Get("ui.task_selection.confirm_btn"))
        {
            myID = 200,
            upNeighborID = 109,
            leftNeighborID = 201,
        };

        _cancelBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Cancel", I18nHelper.Get("ui.task_selection.cancel_btn"))
        {
            myID = 201,
            upNeighborID = 109,
            rightNeighborID = 200,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (MenuScrollBar.TryBeginDrag(_taskListRect, GetVisibleTaskRowCount(), TaskPresentation.TaskOrder.Length, _taskScrollIndex, x, y, out _taskScrollDragOffset))
        {
            _draggingTaskScrollBar = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_taskListRect, TaskPresentation.TaskOrder.Length, GetVisibleTaskRowCount(), x, y))
        {
            ScrollTasks(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_taskListRect, TaskPresentation.TaskOrder.Length, GetVisibleTaskRowCount(), x, y))
        {
            ScrollTasks(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_taskListRect, GetVisibleTaskRowCount(), TaskPresentation.TaskOrder.Length, x, y))
        {
            _taskScrollIndex = MenuScrollBar.GetTrackClickScrollIndex(
                _taskListRect,
                GetVisibleTaskRowCount(),
                TaskPresentation.TaskOrder.Length,
                _taskScrollIndex,
                y);
            BuildComponents();
            populateClickableComponentList();
            return;
        }

        foreach (var row in _toggleRows)
        {
            if (!IsTaskRowVisible(row) || !row.bounds.Contains(x, y))
                continue;

            _onToggleTask(Enum.Parse<TaskKind>(row.name));
            Game1.playSound("smallSelect");
            return;
        }

        if (_nextBtn.bounds.Contains(x, y) && _draft.EnabledTasks.Count > 0)
        {
            _onAdvance(_draft);
            return;
        }

        if (_cancelBtn.bounds.Contains(x, y))
            _onCancel();
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_draggingTaskScrollBar)
            return;

        var next = MenuScrollBar.GetDragScrollIndex(
            _taskListRect,
            GetVisibleTaskRowCount(),
            TaskPresentation.TaskOrder.Length,
            _taskScrollDragOffset,
            y);

        if (next == _taskScrollIndex)
            return;

        _taskScrollIndex = next;
        BuildComponents();
        populateClickableComponentList();
    }

    public override void releaseLeftClick(int x, int y)
    {
        _draggingTaskScrollBar = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B)
        {
            _onCancel();
            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction == 0)
            return;

        ScrollTasks(direction > 0 ? -1 : 1);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.AddRange(_toggleRows);
        allClickableComponents.Add(_nextBtn);
        allClickableComponents.Add(_cancelBtn);
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        if (id is >= 100 and < 100 + 10)
        {
            var taskIndex = id - 100;
            EnsureTaskVisible(taskIndex);
        }

        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void performHoverAction(int x, int y)
    {
        _hoverText = string.Empty;

        for (var i = 0; i < _toggleRows.Count; i++)
        {
            var row = _toggleRows[i];
            if (!IsTaskRowVisible(row) || !row.bounds.Contains(x, y))
                continue;

            var task = TaskPresentation.TaskOrder[i];
            _hoverText = GetRowStateText(task);
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.task_selection.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        for (var i = 0; i < _toggleRows.Count; i++)
        {
            var row = _toggleRows[i];
            if (!IsTaskRowVisible(row))
                continue;

            var enabled = _draft.EnabledTasks.Contains(TaskPresentation.TaskOrder[i]);

            drawTextureBox(b, row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height, Color.White);

            b.Draw(
                Game1.mouseCursors,
                new Rectangle(row.bounds.X + 8, row.bounds.Y + (row.bounds.Height - 36) / 2, 36, 36),
                new Rectangle(enabled ? 236 : 227, 425, 9, 9),
                Color.White);

            Utility.drawTextWithShadow(
                b,
                row.label,
                Game1.smallFont,
                new Vector2(row.bounds.X + 52, row.bounds.Y + 8),
                Game1.textColor);
        }

        MenuScrollBar.Draw(b, _taskListRect, GetVisibleTaskRowCount(), TaskPresentation.TaskOrder.Length, _taskScrollIndex);

        DrawButton(b, _nextBtn, enabled: _draft.EnabledTasks.Count > 0);
        DrawButton(b, _cancelBtn, enabled: true);

        if (!string.IsNullOrWhiteSpace(_hoverText))
            drawHoverText(b, _hoverText, Game1.smallFont);

        drawMouse(b);
    }

    private string GetRowStateText(TaskKind task)
    {
        var row = _draft.PreviewState.ServiceRows.FirstOrDefault(candidate => candidate.Service == task);
        if (row is null)
            return string.Empty;

        return row.RowState switch
        {
            ServiceContributionState.Charged => I18nHelper.Get(
                "ui.task_selection.row_charged",
                new { amount = row.DisplayAmount ?? 0 }),
            ServiceContributionState.NeedsAnimalBuildingScope => I18nHelper.Get("ui.task_selection.row_needs_animal"),
            ServiceContributionState.NeedsGreenhouseScope => I18nHelper.Get("ui.task_selection.row_needs_greenhouse"),
            ServiceContributionState.NeedsOutdoorScope when TaskKindSets.IsGreenhouseService(task)
                && _draft.Greenhouse is null
                && _draft.OutdoorZones.Count == 0 => I18nHelper.Get("ui.task_selection.row_needs_crop_scope"),
            _ => I18nHelper.Get("ui.task_selection.row_needs_outdoor"),
        };
    }

    private int GetVisibleTaskRowCount() => Math.Max(1, _taskListRect.Height / RowHeight);

    private bool IsTaskRowVisible(ClickableComponent row) =>
        row.bounds.Bottom <= _taskListRect.Bottom && row.bounds.Y >= _taskListRect.Y;

    private void ScrollTasks(int delta)
    {
        var next = Math.Clamp(_taskScrollIndex + delta, 0, _maxTaskScrollIndex);
        if (next == _taskScrollIndex)
            return;

        _taskScrollIndex = next;
        BuildComponents();
        populateClickableComponentList();
    }

    private void EnsureTaskVisible(int taskIndex)
    {
        if (taskIndex < _taskScrollIndex)
        {
            _taskScrollIndex = taskIndex;
            BuildComponents();
            populateClickableComponentList();
            return;
        }

        var visibleCount = GetVisibleTaskRowCount();
        var lastVisibleIndex = _taskScrollIndex + visibleCount - 1;
        if (taskIndex <= lastVisibleIndex)
            return;

        _taskScrollIndex = Math.Clamp(taskIndex - visibleCount + 1, 0, _maxTaskScrollIndex);
        BuildComponents();
        populateClickableComponentList();
    }

    private static void DrawButton(SpriteBatch b, ClickableComponent btn, bool enabled)
    {
        var tint = enabled ? Color.White : Color.Gray;
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(
                btn.bounds.X + (int)(btn.bounds.Width - textSize.X) / 2,
                btn.bounds.Y + (int)(btn.bounds.Height - textSize.Y) / 2),
            textTint);
    }
}
