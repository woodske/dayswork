using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class OutputDestinationsMenu : IClickableMenu
{
    private const int MenuWidth = 700;
    private const int MenuHeight = 700;
    private const int OutputRowHeight = 72;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private static readonly TaskKind[] OutputTasks =
    {
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
    };

    private static readonly HashSet<TaskKind> ShippingBinEligible = new()
    {
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.CollectAnimalProducts,
    };

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onAdvance;
    private readonly Action<ContractDraft> _onBack;
    private readonly List<ChestEntry> _chestList;
    private readonly TaskKind[] _enabledOutputTasks;

    private readonly Dictionary<TaskKind, DestinationKey> _outputAssignments = new();
    private readonly List<Rectangle> _outputRowBounds = new();
    private Rectangle _outputListRect;
    private int _outputScrollIndex;
    private int _maxOutputScrollIndex;
    private bool _draggingOutputScrollBar;
    private int _outputScrollDragOffset;
    private string _hoverText = string.Empty;
    private bool _showingPicker;
    private TaskKind _pickerTask;
    private List<(string Label, DestinationKey Dest)> _pickerOptions = new();
    private Rectangle _pickerPanelRect;
    private List<ClickableComponent> _pickerRows = new();

    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _backBtn = null!;
    private readonly List<ClickableComponent> _setOutputBtns = new();

    public OutputDestinationsMenu(
        ContractDraft draft,
        ChestResolver chestResolver,
        Action<ContractDraft> onAdvance,
        Action<ContractDraft> onBack)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft = draft;
        _onAdvance = onAdvance;
        _onBack = onBack;

        _chestList = chestResolver.GetAllChests(Game1.getFarm(), draft.Greenhouses);
        _enabledOutputTasks = OutputTasks.Where(task => draft.EnabledTasks.Contains(task)).ToArray();

        foreach (var (task, destination) in draft.Destinations)
            _outputAssignments[task] = destination;

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        _setOutputBtns.Clear();
        _outputRowBounds.Clear();

        var setLabel = I18nHelper.Get("ui.zone_chest.set_output_btn");
        var setBtnW = (int)Math.Ceiling(Game1.smallFont.MeasureString(setLabel).X) + 40;
        const int setBtnH = 48;
        var btnY = yPositionOnScreen + MenuHeight - 70;

        _outputListRect = new Rectangle(
            xPositionOnScreen + 48,
            yPositionOnScreen + 95,
            MenuWidth - 96 - MenuScrollBar.ReservedWidth,
            btnY - (yPositionOnScreen + 95) - 18);
        _maxOutputScrollIndex = Math.Max(0, _enabledOutputTasks.Length - GetVisibleOutputRowCount());
        _outputScrollIndex = Math.Clamp(_outputScrollIndex, 0, _maxOutputScrollIndex);
        var outY = _outputListRect.Y - _outputScrollIndex * OutputRowHeight;

        for (var i = 0; i < _enabledOutputTasks.Length; i++)
        {
            var rowTop = outY + i * OutputRowHeight;
            _outputRowBounds.Add(new Rectangle(_outputListRect.X, rowTop, _outputListRect.Width, OutputRowHeight - 4));

            var btnTop = rowTop + (OutputRowHeight - setBtnH) / 2;
            _setOutputBtns.Add(new ClickableComponent(
                new Rectangle(_outputListRect.Right - setBtnW, btnTop, setBtnW, setBtnH),
                _enabledOutputTasks[i].ToString(),
                setLabel)
            {
                myID = 200 + i,
                upNeighborID = i > 0 ? 199 + i : 900,
                downNeighborID = i < _enabledOutputTasks.Length - 1 ? 201 + i : 900,
            });
        }

        var lastSetId = _enabledOutputTasks.Length > 0 ? 200 + _enabledOutputTasks.Length - 1 : 900;

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next",
            I18nHelper.Get("ui.zone_chest.confirm_btn"))
        {
            myID = 900,
            upNeighborID = lastSetId,
            leftNeighborID = 901,
        };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.zone_chest.back_btn"))
        {
            myID = 901,
            upNeighborID = lastSetId,
            rightNeighborID = 900,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_showingPicker)
        {
            foreach (var row in _pickerRows)
            {
                if (!row.bounds.Contains(x, y))
                    continue;

                ApplyPickerSelection(_pickerOptions[_pickerRows.IndexOf(row)].Dest);
                Game1.playSound("smallSelect");
                return;
            }

            _showingPicker = false;
            return;
        }

        if (MenuScrollBar.TryBeginDrag(_outputListRect, GetVisibleOutputRowCount(), _enabledOutputTasks.Length, _outputScrollIndex, x, y, out _outputScrollDragOffset))
        {
            _draggingOutputScrollBar = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_outputListRect, _enabledOutputTasks.Length, GetVisibleOutputRowCount(), x, y))
        {
            ScrollOutputs(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_outputListRect, _enabledOutputTasks.Length, GetVisibleOutputRowCount(), x, y))
        {
            ScrollOutputs(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_outputListRect, GetVisibleOutputRowCount(), _enabledOutputTasks.Length, x, y))
        {
            _outputScrollIndex = MenuScrollBar.GetTrackClickScrollIndex(
                _outputListRect,
                GetVisibleOutputRowCount(),
                _enabledOutputTasks.Length,
                _outputScrollIndex,
                y);
            BuildComponents();
            populateClickableComponentList();
            return;
        }

        for (var i = 0; i < _setOutputBtns.Count; i++)
        {
            if (!IsOutputRowVisible(i) || !_setOutputBtns[i].bounds.Contains(x, y))
                continue;

            OpenPicker(_enabledOutputTasks[i], _setOutputBtns[i].bounds);
            Game1.playSound("smallSelect");
            return;
        }

        if (_confirmBtn.bounds.Contains(x, y))
        {
            ConfirmAndAdvance();
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_draggingOutputScrollBar)
            return;

        var next = MenuScrollBar.GetDragScrollIndex(
            _outputListRect,
            GetVisibleOutputRowCount(),
            _enabledOutputTasks.Length,
            _outputScrollDragOffset,
            y);

        if (next == _outputScrollIndex)
            return;

        _outputScrollIndex = next;
        BuildComponents();
        populateClickableComponentList();
    }

    public override void releaseLeftClick(int x, int y)
    {
        _draggingOutputScrollBar = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B)
        {
            if (_showingPicker)
                _showingPicker = false;
            else
                _onBack(_draft);

            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (_showingPicker || direction == 0)
            return;

        ScrollOutputs(direction > 0 ? -1 : 1);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.AddRange(_setOutputBtns);
        allClickableComponents.Add(_confirmBtn);
        allClickableComponents.Add(_backBtn);
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        if (id >= 200 && id < 200 + _enabledOutputTasks.Length)
            EnsureOutputVisible(id - 200);

        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _enabledOutputTasks.Length > 0 ? _setOutputBtns[0] : _confirmBtn;
        snapCursorToCurrentSnappedComponent();
    }

    public override void performHoverAction(int x, int y)
    {
        _hoverText = string.Empty;

        for (var i = 0; i < _enabledOutputTasks.Length; i++)
        {
            if (!IsOutputRowVisible(i) || !_outputRowBounds[i].Contains(x, y))
                continue;

            var taskName = I18nHelper.Get(TaskPresentation.GetI18nKey(_enabledOutputTasks[i]));
            _hoverText = $"{taskName}: {GetDestinationLabel(_enabledOutputTasks[i])}";
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.output_destinations.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        if (_enabledOutputTasks.Length == 0)
        {
            Utility.drawTextWithShadow(
                b,
                I18nHelper.Get("ui.zone_chest.no_output_tasks"),
                Game1.smallFont,
                new Vector2(_outputListRect.X, _outputListRect.Y),
                Color.Gray);
        }
        else
        {
            for (var i = 0; i < _enabledOutputTasks.Length; i++)
            {
                if (!IsOutputRowVisible(i))
                    continue;

                var task = _enabledOutputTasks[i];
                var taskName = I18nHelper.Get(TaskPresentation.GetI18nKey(task));
                var destinationName = GetDestinationLabel(task);
                var row = _outputRowBounds[i];
                var wrappedDestination = Game1.parseText(destinationName, Game1.smallFont, row.Width - _setOutputBtns[i].bounds.Width - 16)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

                Utility.drawTextWithShadow(
                    b,
                    $"{taskName}:",
                    Game1.smallFont,
                    new Vector2(row.X, row.Y + 4),
                    Game1.textColor);

                Utility.drawTextWithShadow(
                    b,
                    wrappedDestination,
                    Game1.smallFont,
                    new Vector2(row.X, row.Y + 28),
                    SecondaryTextColor);

                DrawButton(b, _setOutputBtns[i], enabled: true);
            }
        }

        MenuScrollBar.Draw(b, _outputListRect, GetVisibleOutputRowCount(), _enabledOutputTasks.Length, _outputScrollIndex);
        DrawButton(b, _confirmBtn, enabled: true);
        DrawButton(b, _backBtn, enabled: true);

        if (_showingPicker)
            DrawPicker(b);

        if (!string.IsNullOrWhiteSpace(_hoverText))
            drawHoverText(b, _hoverText, Game1.smallFont);

        drawMouse(b);
    }

    private void OpenPicker(TaskKind task, Rectangle setButtonBounds)
    {
        _pickerTask = task;
        _showingPicker = true;

        _pickerOptions = new List<(string Label, DestinationKey Dest)>
        {
            (I18nHelper.Get("ui.zone_chest.picker_mail_option"), MailDestination.Instance),
        };

        if (ShippingBinEligible.Contains(task))
            _pickerOptions.Add((I18nHelper.Get("ui.zone_chest.shipping_bin_option"), ShippingBinDestination.Instance));

        foreach (var entry in _chestList.Where(entry => IsDestinationEligibleForTask(entry, task)))
            _pickerOptions.Add((entry.DisplayName, new ChestDestination(entry.Ref)));

        const int rowH = 44;
        var maxLabelW = _pickerOptions.Max(option => Game1.smallFont.MeasureString(option.Label).X);
        var panelW = Math.Min((int)Math.Ceiling(maxLabelW) + 32, MenuWidth - 16);
        var panelH = _pickerOptions.Count * rowH + 16;
        var panelX = Math.Max(xPositionOnScreen + 8, setButtonBounds.X - panelW - 8);
        var panelY = Math.Max(yPositionOnScreen + 8, setButtonBounds.Y - panelH / 2);
        panelY = Math.Max(yPositionOnScreen + 8, Math.Min(panelY, yPositionOnScreen + MenuHeight - panelH - 8));

        _pickerPanelRect = new Rectangle(panelX, panelY, panelW, panelH);
        _pickerRows = new List<ClickableComponent>();

        for (var i = 0; i < _pickerOptions.Count; i++)
        {
            _pickerRows.Add(new ClickableComponent(
                new Rectangle(panelX + 8, panelY + 8 + i * rowH, panelW - 16, rowH - 4),
                i.ToString(),
                _pickerOptions[i].Label)
            {
                myID = 600 + i,
            });
        }
    }

    private void ApplyPickerSelection(DestinationKey destination)
    {
        _outputAssignments[_pickerTask] = destination;
        _draft.Destinations[_pickerTask] = destination;
        _showingPicker = false;
    }

    private void ConfirmAndAdvance()
    {
        foreach (var task in _enabledOutputTasks)
        {
            if (!_draft.Destinations.ContainsKey(task))
                _draft.Destinations[task] = MailDestination.Instance;
        }

        _onAdvance(_draft);
    }

    private void DrawPicker(SpriteBatch b)
    {
        drawTextureBox(
            b,
            _pickerPanelRect.X - 4,
            _pickerPanelRect.Y - 4,
            _pickerPanelRect.Width + 8,
            _pickerPanelRect.Height + 8,
            Color.White);

        for (var i = 0; i < _pickerRows.Count; i++)
        {
            var row = _pickerRows[i];
            var destination = _pickerOptions[i].Dest;

            var selected = _outputAssignments.TryGetValue(_pickerTask, out var current)
                           && EqualityComparer<DestinationKey>.Default.Equals(current, destination);
            if (selected)
                b.Draw(Game1.staminaRect, row.bounds, Color.LightBlue * 0.5f);

            Utility.drawTextWithShadow(
                b,
                row.label,
                Game1.smallFont,
                new Vector2(row.bounds.X + 6, row.bounds.Y + (row.bounds.Height - (int)Game1.smallFont.MeasureString("A").Y) / 2),
                selected ? Color.DarkBlue : Game1.textColor);
        }
    }

    private int GetVisibleOutputRowCount() => Math.Max(1, _outputListRect.Height / OutputRowHeight);

    private bool IsOutputRowVisible(int index) =>
        _outputRowBounds[index].Bottom <= _outputListRect.Bottom
        && _outputRowBounds[index].Y >= _outputListRect.Y;

    private void ScrollOutputs(int delta)
    {
        var next = Math.Clamp(_outputScrollIndex + delta, 0, _maxOutputScrollIndex);
        if (next == _outputScrollIndex)
            return;

        _outputScrollIndex = next;
        _showingPicker = false;
        BuildComponents();
        populateClickableComponentList();
    }

    private void EnsureOutputVisible(int index)
    {
        if (index < _outputScrollIndex)
        {
            _outputScrollIndex = index;
            BuildComponents();
            populateClickableComponentList();
            return;
        }

        var visibleCount = GetVisibleOutputRowCount();
        var lastVisibleIndex = _outputScrollIndex + visibleCount - 1;
        if (index <= lastVisibleIndex)
            return;

        _outputScrollIndex = Math.Clamp(index - visibleCount + 1, 0, _maxOutputScrollIndex);
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
                btn.bounds.X + (btn.bounds.Width - (int)textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)textSize.Y) / 2),
            textTint);
    }

    private string GetDestinationLabel(TaskKind task)
    {
        if (!_outputAssignments.TryGetValue(task, out var destination))
            return I18nHelper.Get("ui.zone_chest.no_chest_assigned");

        return destination switch
        {
            ShippingBinDestination => I18nHelper.Get("ui.zone_chest.shipping_bin_option"),
            ChestDestination chest => _chestList.FirstOrDefault(entry => entry.Ref == chest.Ref)?.DisplayName
                                      ?? chest.Ref.ToString(),
            _ => I18nHelper.Get("ui.zone_chest.no_chest_assigned"),
        };
    }

    private static bool IsDestinationEligibleForTask(ChestEntry entry, TaskKind task)
    {
        if (ModEntry.ExpansionCompat is not { } compat ||
            !compat.IsExpansionDepositLocation(entry.Ref.LocationName))
            return true;

        return TaskKindSets.IsGreenhouseService(task);
    }
}
