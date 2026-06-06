using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

/// <summary>A selectable picker row: a primary label plus an optional trailing tag chip.</summary>
internal sealed record PickerRow(string Label, string? Tag);

/// <summary>
/// Reusable scrollable single-select list page used by the Manage Crops authoring flow for the crop,
/// fertilizer, and output-chest pickers (Q2=B). Mouse/keyboard + gamepad; B (or Back) cancels.
/// </summary>
internal sealed class CropListPickerMenu : IClickableMenu
{
    private const int RowHeight = 56;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private readonly string _title;
    private readonly IReadOnlyList<PickerRow> _rows;
    private readonly int _selectedIndex;
    private readonly Action<int> _onSelect;
    private readonly Action _onCancel;

    private readonly List<ClickableComponent> _rowComponents = new();
    private Rectangle _listRect;
    private int _scrollIndex;
    private int _maxScrollIndex;
    private bool _dragging;
    private int _dragOffset;
    private ClickableComponent _backBtn = null!;

    public CropListPickerMenu(
        string title,
        IReadOnlyList<PickerRow> rows,
        int selectedIndex,
        Action<int> onSelect,
        Action onCancel)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _title = title;
        _rows = rows;
        _selectedIndex = selectedIndex;
        _onSelect = onSelect;
        _onCancel = onCancel;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        _rowComponents.Clear();

        var btnY = yPositionOnScreen + height - 70;
        _listRect = new Rectangle(
            xPositionOnScreen + 48,
            yPositionOnScreen + 80,
            width - 96 - MenuScrollBar.ReservedWidth,
            btnY - (yPositionOnScreen + 80) - 18);

        _maxScrollIndex = Math.Max(0, _rows.Count - GetVisibleRowCount());
        _scrollIndex = Math.Clamp(_scrollIndex, 0, _maxScrollIndex);
        var top = _listRect.Y - _scrollIndex * RowHeight;

        for (var i = 0; i < _rows.Count; i++)
        {
            _rowComponents.Add(new ClickableComponent(
                new Rectangle(_listRect.X, top + i * RowHeight, _listRect.Width, RowHeight - 4),
                i.ToString(),
                _rows[i].Label)
            {
                myID = 700 + i,
                upNeighborID = i > 0 ? 700 + i - 1 : 901,
                downNeighborID = i < _rows.Count - 1 ? 700 + i + 1 : 901,
            });
        }

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.common.back_btn"))
        {
            myID = 901,
            upNeighborID = _rows.Count > 0 ? 700 : -1,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (MenuScrollBar.TryBeginDrag(_listRect, GetVisibleRowCount(), _rows.Count, _scrollIndex, x, y, out _dragOffset))
        {
            _dragging = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_listRect, _rows.Count, GetVisibleRowCount(), x, y))
        {
            Scroll(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_listRect, _rows.Count, GetVisibleRowCount(), x, y))
        {
            Scroll(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_listRect, GetVisibleRowCount(), _rows.Count, x, y))
        {
            _scrollIndex = MenuScrollBar.GetTrackClickScrollIndex(_listRect, GetVisibleRowCount(), _rows.Count, _scrollIndex, y);
            BuildComponents();
            populateClickableComponentList();
            return;
        }

        for (var i = 0; i < _rowComponents.Count; i++)
        {
            if (!IsRowVisible(i) || !_rowComponents[i].bounds.Contains(x, y))
                continue;

            Game1.playSound("smallSelect");
            _onSelect(i);
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
        {
            Game1.playSound("bigDeSelect");
            _onCancel();
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_dragging)
            return;

        var next = MenuScrollBar.GetDragScrollIndex(_listRect, GetVisibleRowCount(), _rows.Count, _dragOffset, y);
        if (next == _scrollIndex)
            return;

        _scrollIndex = next;
        BuildComponents();
        populateClickableComponentList();
    }

    public override void releaseLeftClick(int x, int y)
    {
        _dragging = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction != 0)
            Scroll(direction > 0 ? -1 : 1);
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

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.AddRange(_rowComponents);
        allClickableComponents.Add(_backBtn);
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        if (id >= 700 && id < 700 + _rows.Count)
            EnsureVisible(id - 700);

        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        if (_rows.Count > 0)
        {
            var target = Math.Clamp(_selectedIndex, 0, _rows.Count - 1);
            EnsureVisible(target);
            currentlySnappedComponent = getComponentWithID(700 + target) ?? _backBtn;
        }
        else
        {
            currentlySnappedComponent = _backBtn;
        }

        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            _title,
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        if (_rows.Count == 0)
        {
            Utility.drawTextWithShadow(
                b,
                I18nHelper.Get("ui.manage_crops.picker_empty"),
                Game1.smallFont,
                new Vector2(_listRect.X, _listRect.Y),
                Color.Gray);
        }

        for (var i = 0; i < _rowComponents.Count; i++)
        {
            if (!IsRowVisible(i))
                continue;

            var row = _rowComponents[i];
            if (i == _selectedIndex)
                b.Draw(Game1.staminaRect, row.bounds, Color.LightBlue * 0.4f);

            Utility.drawTextWithShadow(
                b,
                _rows[i].Label,
                Game1.smallFont,
                new Vector2(row.bounds.X + 12, row.bounds.Y + (RowHeight - (int)Game1.smallFont.MeasureString(_rows[i].Label).Y) / 2),
                i == _selectedIndex ? Color.DarkBlue : Game1.textColor);

            if (_rows[i].Tag is { } tag)
            {
                var tagSize = Game1.smallFont.MeasureString(tag);
                Utility.drawTextWithShadow(
                    b,
                    tag,
                    Game1.smallFont,
                    new Vector2(row.bounds.Right - tagSize.X - 12, row.bounds.Y + (RowHeight - (int)tagSize.Y) / 2),
                    SecondaryTextColor);
            }
        }

        MenuScrollBar.Draw(b, _listRect, GetVisibleRowCount(), _rows.Count, _scrollIndex);
        DrawButton(b, _backBtn);
        drawMouse(b);
    }

    private int GetVisibleRowCount() => Math.Max(1, _listRect.Height / RowHeight);

    private bool IsRowVisible(int index) =>
        _rowComponents[index].bounds.Bottom <= _listRect.Bottom
        && _rowComponents[index].bounds.Y >= _listRect.Y;

    private void Scroll(int delta)
    {
        var next = Math.Clamp(_scrollIndex + delta, 0, _maxScrollIndex);
        if (next == _scrollIndex)
            return;

        _scrollIndex = next;
        BuildComponents();
        populateClickableComponentList();
    }

    private void EnsureVisible(int index)
    {
        if (index < _scrollIndex)
        {
            _scrollIndex = index;
        }
        else
        {
            var lastVisible = _scrollIndex + GetVisibleRowCount() - 1;
            if (index > lastVisible)
                _scrollIndex = Math.Clamp(index - GetVisibleRowCount() + 1, 0, _maxScrollIndex);
            else
                return;
        }

        BuildComponents();
        populateClickableComponentList();
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
}
