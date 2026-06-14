using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class ZoneAndChestMenu : IClickableMenu
{
    private const int SummaryLineSpacing = 28;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<ContractDraft> _onBeginZoneDraw;
    private readonly Action<ContractDraft> _onClearScope;
    private readonly List<ScopeLine> _summaryLines = new();

    private ClickableComponent _workAreaBtn = null!;
    private ClickableComponent _clearScopeBtn = null!;
    private ClickableComponent _backBtn = null!;
    private Rectangle _summaryRect;
    private int _summaryScrollIndex;
    private int _maxSummaryScrollIndex;
    private bool _draggingSummaryScrollBar;
    private int _summaryScrollDragOffset;

    public ZoneAndChestMenu(
        ContractDraft draft,
        ChestResolver chestResolver,
        Action<ContractDraft> onBack,
        Action<ContractDraft> onBeginZoneDraw,
        Action<ContractDraft> onClearScope)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _draft = draft;
        _onBack = onBack;
        _onBeginZoneDraw = onBeginZoneDraw;
        _onClearScope = onClearScope;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        var x0 = xPositionOnScreen + 48;
        var zoneY = yPositionOnScreen + 95;

        var workAreaLabel = I18nHelper.Get("ui.zone_chest.select_work_area_btn");
        var clearLabel = I18nHelper.Get("ui.zone_chest.clear_scope_btn");
        var btnW = (int)Math.Ceiling(Math.Max(
            Game1.smallFont.MeasureString(workAreaLabel).X,
            Game1.smallFont.MeasureString(clearLabel).X)) + 56;
        const int btnH = 56;
        const int btnGap = 16;

        _workAreaBtn = new ClickableComponent(
            new Rectangle(x0, zoneY, btnW, btnH),
            "WorkArea",
            workAreaLabel)
        {
            myID = 100,
            rightNeighborID = 101,
            downNeighborID = 901,
        };

        _clearScopeBtn = new ClickableComponent(
            new Rectangle(x0 + btnW + btnGap, zoneY, btnW, btnH),
            "ClearScope",
            clearLabel)
        {
            myID = 101,
            leftNeighborID = 100,
            downNeighborID = 901,
        };

        var btnY = yPositionOnScreen + height - 70;
        var summaryTop = yPositionOnScreen + 190;
        _summaryRect = new Rectangle(
            xPositionOnScreen + 48,
            summaryTop,
            width - 96 - MenuScrollBar.ReservedWidth,
            btnY - summaryTop - 18);
        BuildSummaryLines();
        _maxSummaryScrollIndex = Math.Max(0, _summaryLines.Count - GetVisibleSummaryLineCount());
        _summaryScrollIndex = Math.Clamp(_summaryScrollIndex, 0, _maxSummaryScrollIndex);

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.common.back_btn"))
        {
            myID = 901,
            upNeighborID = 100,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (MenuScrollBar.TryBeginDrag(
                _summaryRect,
                GetVisibleSummaryLineCount(),
                _summaryLines.Count,
                _summaryScrollIndex,
                x,
                y,
                out _summaryScrollDragOffset))
        {
            _draggingSummaryScrollBar = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_summaryRect, _summaryLines.Count, GetVisibleSummaryLineCount(), x, y))
        {
            ScrollSummary(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_summaryRect, _summaryLines.Count, GetVisibleSummaryLineCount(), x, y))
        {
            ScrollSummary(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_summaryRect, GetVisibleSummaryLineCount(), _summaryLines.Count, x, y))
        {
            _summaryScrollIndex = MenuScrollBar.GetTrackClickScrollIndex(
                _summaryRect,
                GetVisibleSummaryLineCount(),
                _summaryLines.Count,
                _summaryScrollIndex,
                y);
            return;
        }

        if (_workAreaBtn.bounds.Contains(x, y))
        {
            Game1.playSound("smallSelect");
            _onBeginZoneDraw(_draft);
            return;
        }

        if (_clearScopeBtn.bounds.Contains(x, y))
        {
            _onClearScope(_draft);
            Game1.playSound("trashcan");
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_draggingSummaryScrollBar)
            return;

        _summaryScrollIndex = MenuScrollBar.GetDragScrollIndex(
            _summaryRect,
            GetVisibleSummaryLineCount(),
            _summaryLines.Count,
            _summaryScrollDragOffset,
            y);
    }

    public override void releaseLeftClick(int x, int y)
    {
        _draggingSummaryScrollBar = false;
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

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction != 0)
            ScrollSummary(direction > 0 ? -1 : 1);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.Add(_workAreaBtn);
        allClickableComponents.Add(_clearScopeBtn);
        allClickableComponents.Add(_backBtn);
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _workAreaBtn;
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.zone_chest.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        DrawButton(b, _workAreaBtn, enabled: true);
        DrawButton(b, _clearScopeBtn, enabled: HasSelectedScope());
        DrawSummary(b);
        MenuScrollBar.Draw(b, _summaryRect, GetVisibleSummaryLineCount(), _summaryLines.Count, _summaryScrollIndex);

        DrawButton(b, _backBtn, enabled: true);
        drawMouse(b);
    }

    private void DrawSummary(SpriteBatch b)
    {
        var lineY = _summaryRect.Y;
        var visibleLineCount = GetVisibleSummaryLineCount();
        for (var i = _summaryScrollIndex; i < _summaryLines.Count && i < _summaryScrollIndex + visibleLineCount; i++)
        {
            Utility.drawTextWithShadow(
                b,
                _summaryLines[i].Text,
                Game1.smallFont,
                new Vector2(_summaryRect.X, lineY),
                _summaryLines[i].Color);
            lineY += SummaryLineSpacing;
        }
    }

    private void BuildSummaryLines()
    {
        _summaryLines.Clear();

        AddSummarySection(
            I18nHelper.Get("ui.zone_chest.outdoor_section_label"),
            I18nHelper.Get("ui.zone_chest.outdoor_count_label", new { count = _draft.PreviewState.ScopeSummary.OutdoorZones.Count }));
        AddSummarySection(
            I18nHelper.Get("ui.zone_chest.animal_section_label"),
            FormatAnimalScopeSummary(),
            I18nHelper.Get("ui.zone_chest.animal_scope_detail"));
        AddSummarySection(
            I18nHelper.Get("ui.zone_chest.greenhouse_section_label"),
            _draft.PreviewState.ScopeSummary.Greenhouses.Count == 0
                ? I18nHelper.Get("ui.zone_chest.greenhouse_not_selected")
                : I18nHelper.Get(
                    "ui.zone_chest.greenhouse_selected",
                    new { location = FormatGreenhouseSummary(_draft.PreviewState.ScopeSummary.Greenhouses) }),
            I18nHelper.Get("ui.zone_chest.greenhouse_scope_detail"));
    }

    private void AddSummarySection(string label, string value, string? detail = null)
    {
        AddWrappedSummaryLine(label, Game1.textColor);
        AddWrappedSummaryLine(value, SecondaryTextColor);

        if (!string.IsNullOrWhiteSpace(detail))
            AddWrappedSummaryLine(detail, SecondaryTextColor * 0.9f);

        _summaryLines.Add(new ScopeLine(string.Empty, SecondaryTextColor));
    }

    private void AddWrappedSummaryLine(string text, Color color)
    {
        var wrapped = Game1.parseText(text, Game1.smallFont, _summaryRect.Width)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        foreach (var line in wrapped.Split('\n'))
            _summaryLines.Add(new ScopeLine(line, color));
    }

    private int GetVisibleSummaryLineCount() =>
        ContractMenuViewport.GetVisibleCount(_summaryRect.Height, SummaryLineSpacing);

    private void ScrollSummary(int delta)
    {
        var next = Math.Clamp(_summaryScrollIndex + delta, 0, _maxSummaryScrollIndex);
        if (next != _summaryScrollIndex)
            _summaryScrollIndex = next;
    }

    private bool HasSelectedScope() =>
        _draft.OutdoorZones.Count > 0
        || _draft.AnimalBuildings.Count > 0
        || _draft.Greenhouses.Count > 0;

    private static string FormatGreenhouseSummary(IReadOnlyList<GreenhouseSelection> greenhouses) =>
        string.Join(
            ", ",
            greenhouses
                .Select(greenhouse => FriendlyBuildingName(greenhouse.LocationName))
                .OrderBy(name => name, StringComparer.Ordinal));

    private string FormatAnimalScopeSummary()
    {
        if (_draft.PreviewState.ScopeSummary.AnimalBuildings.Count == 0)
            return I18nHelper.Get("ui.zone_chest.none_selected");

        return string.Join(
            ", ",
            _draft.PreviewState.ScopeSummary.AnimalBuildings
                .Select(building => FriendlyBuildingName(building.LocationName))
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    // Selection LocationNames are unique interior names (type + a trailing GUID). Strip the
    // GUID for display so the player sees "Coop"/"Barn" rather than "Coop1bc97e5f-…".
    private static string FriendlyBuildingName(string locationName)
    {
        if (locationName.Length > 36 && Guid.TryParse(locationName[^36..], out _))
            return locationName[..^36];

        return locationName;
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

    private readonly record struct ScopeLine(string Text, Color Color);
}
