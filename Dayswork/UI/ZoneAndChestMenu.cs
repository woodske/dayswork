using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class ZoneAndChestMenu : IClickableMenu
{
    private const int MenuWidth = 700;
    private const int MenuHeight = 700;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onAdvance;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<ContractDraft> _onBeginZoneDraw;
    private readonly Action<ContractDraft> _onClearScope;

    private ClickableComponent _workAreaBtn = null!;
    private ClickableComponent _clearScopeBtn = null!;
    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _backBtn = null!;

    public ZoneAndChestMenu(
        ContractDraft draft,
        ChestResolver chestResolver,
        Action<ContractDraft> onAdvance,
        Action<ContractDraft> onBack,
        Action<ContractDraft> onBeginZoneDraw,
        Action<ContractDraft> onClearScope)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft = draft;
        _onAdvance = onAdvance;
        _onBack = onBack;
        _onBeginZoneDraw = onBeginZoneDraw;
        _onClearScope = onClearScope;

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
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
            downNeighborID = 900,
        };

        _clearScopeBtn = new ClickableComponent(
            new Rectangle(x0 + btnW + btnGap, zoneY, btnW, btnH),
            "ClearScope",
            clearLabel)
        {
            myID = 101,
            leftNeighborID = 100,
            downNeighborID = 900,
        };

        var btnY = yPositionOnScreen + MenuHeight - 70;

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next",
            I18nHelper.Get("ui.zone_chest.confirm_btn"))
        {
            myID = 900,
            upNeighborID = 100,
            leftNeighborID = 901,
        };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.zone_chest.back_btn"))
        {
            myID = 901,
            upNeighborID = 100,
            rightNeighborID = 900,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
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

        if (_confirmBtn.bounds.Contains(x, y))
        {
            _onAdvance(_draft);
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
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
        allClickableComponents.Add(_workAreaBtn);
        allClickableComponents.Add(_clearScopeBtn);
        allClickableComponents.Add(_confirmBtn);
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

        const int leftMargin = 48;
        const float sectionGap = 24f;
        var sectionY = yPositionOnScreen + 190f;

        sectionY += DrawScopeSection(
            b,
            I18nHelper.Get("ui.zone_chest.outdoor_section_label"),
            I18nHelper.Get("ui.zone_chest.outdoor_count_label", new { count = _draft.PreviewState.ScopeSummary.OutdoorZones.Count }),
            new Vector2(xPositionOnScreen + leftMargin, sectionY));
        sectionY += sectionGap;

        sectionY += DrawScopeSection(
            b,
            I18nHelper.Get("ui.zone_chest.animal_section_label"),
            FormatAnimalScopeSummary(),
            new Vector2(xPositionOnScreen + leftMargin, sectionY),
            I18nHelper.Get("ui.zone_chest.animal_scope_detail"));
        sectionY += sectionGap;

        DrawScopeSection(
            b,
            I18nHelper.Get("ui.zone_chest.greenhouse_section_label"),
            _draft.PreviewState.ScopeSummary.Greenhouse is null
                ? I18nHelper.Get("ui.zone_chest.greenhouse_not_selected")
                : I18nHelper.Get(
                    "ui.zone_chest.greenhouse_selected",
                    new { location = _draft.PreviewState.ScopeSummary.Greenhouse.LocationName }),
            new Vector2(xPositionOnScreen + leftMargin, sectionY),
            I18nHelper.Get("ui.zone_chest.greenhouse_scope_detail"));

        DrawButton(b, _confirmBtn, enabled: true);
        DrawButton(b, _backBtn, enabled: true);
        drawMouse(b);
    }

    // Draws a scope section (label + wrapped value + optional wrapped detail) and returns the total
    // height consumed, so the caller can lay the next section out below it. Wrapping keeps long
    // animal-building lists inside the panel instead of running off the right edge.
    private float DrawScopeSection(SpriteBatch b, string label, string value, Vector2 position, string? detail = null)
    {
        var maxWidth = Math.Max(120, width - 96);

        Utility.drawTextWithShadow(b, label, Game1.smallFont, position, Game1.textColor);
        var y = position.Y + 30f;

        var wrappedValue = Game1.parseText(value, Game1.smallFont, maxWidth);
        Utility.drawTextWithShadow(b, wrappedValue, Game1.smallFont, new Vector2(position.X, y), SecondaryTextColor);
        y += Game1.smallFont.MeasureString(wrappedValue).Y + 6f;

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var wrappedDetail = Game1.parseText(detail, Game1.smallFont, maxWidth);
            Utility.drawTextWithShadow(b, wrappedDetail, Game1.smallFont, new Vector2(position.X, y), SecondaryTextColor * 0.9f);
            y += Game1.smallFont.MeasureString(wrappedDetail).Y;
        }

        return y - position.Y;
    }

    private bool HasSelectedScope() =>
        _draft.OutdoorZones.Count > 0
        || _draft.AnimalBuildings.Count > 0
        || _draft.Greenhouse is not null;

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

    // Selection LocationNames are unique interior names (type + a trailing GUID, TODO-08). Strip the
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
}
