using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class OutputDestinationsMenu : IClickableMenu
{
    private const int OutputRowHeight = 72;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private static readonly TaskKind[] OutputTasks =
    {
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.HarvestCave,
        TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
    };

    private static readonly HashSet<TaskKind> ShippingBinEligible = new()
    {
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.HarvestCave,
        TaskKind.CollectAnimalProducts,
    };

    private readonly ContractDraft _draft;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;
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

    private ClickableComponent _backBtn = null!;
    private readonly List<ClickableComponent> _setOutputBtns = new();

    public OutputDestinationsMenu(
        ContractDraft draft,
        ChestResolver chestResolver,
        IModHelper helper,
        Action<ContractDraft> onBack)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _draft = draft;
        _chestResolver = chestResolver;
        _helper = helper;
        _onBack = onBack;

        _chestList = chestResolver.GetAllChests(Game1.getFarm(), draft.Greenhouses);
        _enabledOutputTasks = OutputTasks.Where(task => draft.EnabledTasks.Contains(task)).ToArray();

        foreach (var (task, destination) in draft.Destinations)
            _outputAssignments[task] = destination;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
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
        var btnY = yPositionOnScreen + height - 70;

        _outputListRect = new Rectangle(
            xPositionOnScreen + 48,
            yPositionOnScreen + 95,
            width - 96 - MenuScrollBar.ReservedWidth,
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
                upNeighborID = i > 0 ? 199 + i : 901,
                downNeighborID = i < _enabledOutputTasks.Length - 1 ? 201 + i : 901,
            });
        }

        var lastSetId = _enabledOutputTasks.Length > 0 ? 200 + _enabledOutputTasks.Length - 1 : 901;

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.common.back_btn"))
        {
            myID = 901,
            upNeighborID = lastSetId,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
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

            LaunchChestPicker(_enabledOutputTasks[i]);
            Game1.playSound("smallSelect");
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            ApplyDefaultsAndBack();
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
            ApplyDefaultsAndBack();
            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction == 0)
            return;

        ScrollOutputs(direction > 0 ? -1 : 1);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.AddRange(_setOutputBtns);
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
        currentlySnappedComponent = _enabledOutputTasks.Length > 0 ? _setOutputBtns[0] : _backBtn;
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
        DrawButton(b, _backBtn, enabled: true);

        if (!string.IsNullOrWhiteSpace(_hoverText))
            drawHoverText(b, _hoverText, Game1.smallFont);

        drawMouse(b);
    }

    // Opens the map-based chest picker for one output task. Chests not eligible for this task
    // (expansion deposit locations for non-greenhouse work) are filtered out before grouping.
    private void LaunchChestPicker(TaskKind task)
    {
        var locations = ChestMapLocation.Group(
            _chestList.Where(entry => IsDestinationEligibleForTask(entry, task)));
        _outputAssignments.TryGetValue(task, out var current);

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initial: current,
            options: new ChestPickerOptions(
                ShowAutomatic: true,
                ShowShippingBin: ShippingBinEligible.Contains(task),
                ShowNone: false),
            onComplete: destination =>
            {
                ApplyPickerSelection(task, destination ?? AutomaticOutputDestination.Instance);
                Game1.activeClickableMenu = this;
            },
            onCancel: () => Game1.activeClickableMenu = this);
    }

    private void ApplyPickerSelection(TaskKind task, DestinationKey destination)
    {
        _outputAssignments[task] = destination;
        _draft.Destinations[task] = destination;
    }

    // Returning to the hub locks in a default (cabin chest) for any output task left unset, matching
    // the old "Next" behavior so output is always routed somewhere.
    private void ApplyDefaultsAndBack()
    {
        foreach (var task in _enabledOutputTasks)
        {
            if (!_draft.Destinations.ContainsKey(task))
                _draft.Destinations[task] = AutomaticOutputDestination.Instance;
        }

        _onBack(_draft);
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
            return I18nHelper.Get("ui.zone_chest.automatic_output_label");

        return destination switch
        {
            ShippingBinDestination => I18nHelper.Get("ui.zone_chest.shipping_bin_option"),
            ChestDestination chest => _chestList.FirstOrDefault(entry => entry.Ref == chest.Ref)?.DisplayName
                                      ?? chest.Ref.ToString(),
            _ => I18nHelper.Get("ui.zone_chest.automatic_output_label"),
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
