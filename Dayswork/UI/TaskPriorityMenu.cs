using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Task Priority page — reorder the work categories the farmhand finishes first. Supports mouse
// drag-and-drop and up/down arrow buttons (gamepad/keyboard fallback). Mutates
// ContractDraft.CategoryPriority directly; Back returns to the hub.
internal sealed class TaskPriorityMenu : IClickableMenu
{
    private const int RowHeight = 64;
    private const int RowGap = 12;
    private const int ArrowSize = 36;

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onChanged;
    private readonly Action<ContractDraft> _onBack;

    private readonly List<CategoryRow> _rows = new();
    private ClickableComponent _backBtn = null!;

    private int _rowsTop;
    private int _draggingIndex = -1;
    private int _dragGrabOffset;
    private int _dragMouseY;

    public TaskPriorityMenu(
        ContractDraft draft,
        Action<ContractDraft> onChanged,
        Action<ContractDraft> onBack)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _draft = draft;
        _onChanged = onChanged;
        _onBack = onBack;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private int RowStride => RowHeight + RowGap;

    private void BuildComponents()
    {
        _rows.Clear();
        _rowsTop = yPositionOnScreen + 170;

        var rowX = xPositionOnScreen + 48;
        var rowW = width - 96;

        for (var i = 0; i < _draft.CategoryPriority.Count; i++)
        {
            var rowY = _rowsTop + i * RowStride;
            var body = new ClickableComponent(
                new Rectangle(rowX, rowY, rowW, RowHeight),
                _draft.CategoryPriority[i].ToString(),
                string.Empty);

            var arrowX = rowX + rowW - ArrowSize - 16;
            var up = new ClickableComponent(
                new Rectangle(arrowX - ArrowSize - 8, rowY + (RowHeight - ArrowSize) / 2, ArrowSize, ArrowSize),
                $"Up{i}",
                "^") { myID = 720 + i * 2 };
            var down = new ClickableComponent(
                new Rectangle(arrowX, rowY + (RowHeight - ArrowSize) / 2, ArrowSize, ArrowSize),
                $"Down{i}",
                "v") { myID = 721 + i * 2 };

            _rows.Add(new CategoryRow(_draft.CategoryPriority[i], i, body, up, down));
        }

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + height - 70, 170, 56),
            "Back",
            I18nHelper.Get("ui.common.back_btn"))
        {
            myID = 901,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var row in _rows)
        {
            if (row.Index > 0 && row.Up.bounds.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                _draft.MoveCategory(row.Category, -1);
                Refresh();
                return;
            }

            if (row.Index < _rows.Count - 1 && row.Down.bounds.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                _draft.MoveCategory(row.Category, 1);
                Refresh();
                return;
            }
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Body.bounds.Contains(x, y))
                continue;

            _draggingIndex = i;
            _dragGrabOffset = y - _rows[i].Body.bounds.Y;
            _dragMouseY = y;
            Game1.playSound("dwop");
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (_draggingIndex >= 0)
            _dragMouseY = y;
    }

    public override void releaseLeftClick(int x, int y)
    {
        if (_draggingIndex >= 0)
        {
            var targetIndex = TargetIndexForDrag();
            if (targetIndex != _draggingIndex)
            {
                Game1.playSound("smallSelect");
                _draft.MoveCategoryToIndex(_rows[_draggingIndex].Category, targetIndex);
                Refresh();
            }

            _draggingIndex = -1;
        }

        base.releaseLeftClick(x, y);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B)
        {
            _onBack(_draft);
            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        foreach (var row in _rows)
        {
            allClickableComponents.Add(row.Up);
            allClickableComponents.Add(row.Down);
        }

        allClickableComponents.Add(_backBtn);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _rows.Count > 0 ? _rows[0].Down : _backBtn;
        snapCursorToCurrentSnappedComponent();
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.priority.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        var wrappedDescription = Game1.parseText(I18nHelper.Get("ui.priority.description"), Game1.smallFont, width - 96);
        Utility.drawTextWithShadow(
            b,
            wrappedDescription,
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, yPositionOnScreen + 72),
            Game1.textColor);

        var insertionIndex = _draggingIndex >= 0 ? TargetIndexForDrag() : -1;

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i == _draggingIndex)
                continue;

            DrawRow(b, _rows[i], i, highlight: i == insertionIndex);
        }

        // Dragged row follows the cursor and is drawn last so it floats above the others.
        if (_draggingIndex >= 0)
        {
            var dragged = _rows[_draggingIndex];
            var floatY = Math.Clamp(_dragMouseY - _dragGrabOffset, _rowsTop, _rowsTop + (_rows.Count - 1) * RowStride);
            DrawRowAt(b, dragged, floatY, Color.LightSkyBlue);
        }

        DrawButton(b, _backBtn);
        drawMouse(b);
    }

    private int TargetIndexForDrag()
    {
        var relative = _dragMouseY - _dragGrabOffset - _rowsTop;
        var index = (int)Math.Round(relative / (double)RowStride);
        return Math.Clamp(index, 0, _rows.Count - 1);
    }

    private void DrawRow(SpriteBatch b, CategoryRow row, int displayIndex, bool highlight)
    {
        DrawRowAt(b, row, _rowsTop + displayIndex * RowStride, highlight ? Color.Wheat : Color.White);
        DrawArrow(b, row.Up, row.Index > 0);
        DrawArrow(b, row.Down, row.Index < _rows.Count - 1);
    }

    private void DrawRowAt(SpriteBatch b, CategoryRow row, int rowY, Color boxColor)
    {
        var bounds = row.Body.bounds;
        drawTextureBox(b, bounds.X, rowY, bounds.Width, RowHeight, boxColor);

        var label = $"{row.Index + 1}. {I18nHelper.Get($"ui.summary.category.{CategoryKey(row.Category)}")}";
        Utility.drawTextWithShadow(
            b,
            label,
            Game1.smallFont,
            new Vector2(bounds.X + 20, rowY + (RowHeight - (int)Game1.smallFont.MeasureString(label).Y) / 2),
            Game1.textColor);
    }

    private void Refresh()
    {
        _onChanged(_draft);
        BuildComponents();
        populateClickableComponentList();
    }

    private static string CategoryKey(TaskCategory category) => category switch
    {
        TaskCategory.AnimalCare => "animal_care",
        TaskCategory.Crops => "crops",
        TaskCategory.Fieldwork => "fieldwork",
        TaskCategory.Machines => "machines",
        _ => category.ToString(),
    };

    private static void DrawArrow(SpriteBatch b, ClickableComponent btn, bool enabled)
    {
        var tint = enabled ? Color.White : Color.Gray * 0.5f;
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width - textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - textSize.Y) / 2),
            textTint);
    }

    private static void DrawButton(SpriteBatch b, ClickableComponent btn)
    {
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width - (int)textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)textSize.Y) / 2),
            Game1.textColor);
    }

    private sealed record CategoryRow(
        TaskCategory Category,
        int Index,
        ClickableComponent Body,
        ClickableComponent Up,
        ClickableComponent Down);
}
