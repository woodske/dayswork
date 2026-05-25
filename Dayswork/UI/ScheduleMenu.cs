using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Screen 3 of the hiring flow — one-time vs. recurring schedule selection (M-06).
// Selected option is stored in ContractDraft.Schedule. draw() reads only pre-computed fields.
internal sealed class ScheduleMenu : IClickableMenu
{
    private const int MenuWidth  = 700;
    private const int MenuHeight = 380;
    private const int CardWidth  = 290;
    private const int CardHeight = 200;

    private readonly ContractDraft          _draft;
    private readonly Action<ContractDraft, ContractSchedule> _onScheduleChanged;
    private readonly Action<ContractDraft>  _onAdvance;
    private readonly Action<ContractDraft>  _onBack;

    // Pre-computed labels (i18n keys resolved once in ctor — never in draw())
    private readonly string _titleText;
    private readonly string _oneTimeLabel;
    private readonly string _oneTimeDesc;
    private readonly string _recurringLabel;
    private readonly string _recurringDesc;
    private readonly string _nextLabel;
    private readonly string _backLabel;

    private ClickableComponent _oneTimeCard   = null!;
    private ClickableComponent _recurringCard = null!;
    private ClickableComponent _nextBtn       = null!;
    private ClickableComponent _backBtn       = null!;

    internal ScheduleMenu(
        ContractDraft          draft,
        Action<ContractDraft, ContractSchedule> onScheduleChanged,
        Action<ContractDraft>  onAdvance,
        Action<ContractDraft>  onBack)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft     = draft;
        _onScheduleChanged = onScheduleChanged;
        _onAdvance = onAdvance;
        _onBack    = onBack;

        _titleText      = I18nHelper.Get("ui.schedule.title");
        _oneTimeLabel   = I18nHelper.Get("ui.schedule.one_time");
        _oneTimeDesc    = I18nHelper.Get("ui.schedule.one_time_description");
        _recurringLabel = I18nHelper.Get("ui.schedule.recurring");
        _recurringDesc  = I18nHelper.Get("ui.schedule.recurring_description");
        _nextLabel      = I18nHelper.Get("ui.schedule.confirm_btn");
        _backLabel      = I18nHelper.Get("ui.schedule.back_btn");

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        int cardY = yPositionOnScreen + 80;
        int leftCardX  = xPositionOnScreen + 40;
        int rightCardX = xPositionOnScreen + MenuWidth - CardWidth - 40;

        _oneTimeCard = new ClickableComponent(
            new Rectangle(leftCardX, cardY, CardWidth, CardHeight),
            "OneTime", _oneTimeLabel)
        {
            myID            = 100,
            rightNeighborID = 101,
            downNeighborID  = 300,
        };

        _recurringCard = new ClickableComponent(
            new Rectangle(rightCardX, cardY, CardWidth, CardHeight),
            "Recurring", _recurringLabel)
        {
            myID           = 101,
            leftNeighborID = 100,
            downNeighborID = 300,
        };

        int btnY = yPositionOnScreen + MenuHeight - 70;

        _nextBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next", _nextLabel)
        {
            myID           = 300,
            leftNeighborID = 301,
            upNeighborID   = 100,
        };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back", _backLabel)
        {
            myID            = 301,
            rightNeighborID = 300,
            upNeighborID    = 101,
        };
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_oneTimeCard.bounds.Contains(x, y))
        {
            _onScheduleChanged(_draft, ContractSchedule.OneTime);
            return;
        }
        if (_recurringCard.bounds.Contains(x, y))
        {
            _onScheduleChanged(_draft, ContractSchedule.Recurring);
            return;
        }
        if (_nextBtn.bounds.Contains(x, y)) { _onAdvance(_draft); return; }
        if (_backBtn.bounds.Contains(x, y)) { _onBack(_draft);    return; }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { _onBack(_draft); return; }
        base.receiveGamePadButton(b);
    }

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.Add(_oneTimeCard);
        allClickableComponents.Add(_recurringCard);
        allClickableComponents.Add(_nextBtn);
        allClickableComponents.Add(_backBtn);
    }

    public override void snapToDefaultClickableComponent()
    {
        // Snap to whichever card is currently selected.
        int id = _draft.Schedule == ContractSchedule.OneTime ? 100 : 101;
        currentlySnappedComponent = getComponentWithID(id);
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
        // Panel background
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        // Title
        Utility.drawTextWithShadow(
            b,
            _titleText,
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        // Option cards
        DrawCard(b, _oneTimeCard, _oneTimeLabel, _oneTimeDesc,
            selected: _draft.Schedule == ContractSchedule.OneTime);
        DrawCard(b, _recurringCard, _recurringLabel, _recurringDesc,
            selected: _draft.Schedule == ContractSchedule.Recurring);

        // Navigation buttons
        DrawButton(b, _nextBtn);
        DrawButton(b, _backBtn);

        drawMouse(b);
    }

    private static void DrawCard(SpriteBatch b, ClickableComponent card,
        string label, string description, bool selected)
    {
        var cardColor = selected ? Color.LightSkyBlue : Color.White;
        drawTextureBox(b, card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height, cardColor);

        // Card title
        Utility.drawTextWithShadow(
            b,
            label,
            Game1.smallFont,
            new Vector2(card.bounds.X + 16, card.bounds.Y + 16),
            selected ? Color.DarkBlue : Game1.textColor);

        // Card description — word-wrapped to card width with left padding
        var wrapped = Game1.parseText(description, Game1.smallFont, card.bounds.Width - 32);
        Utility.drawTextWithShadow(
            b,
            wrapped,
            Game1.smallFont,
            new Vector2(card.bounds.X + 16, card.bounds.Y + 60),
            Game1.textColor);
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
                btn.bounds.X + (int)(btn.bounds.Width  - textSize.X) / 2,
                btn.bounds.Y + (int)(btn.bounds.Height - textSize.Y) / 2),
            Game1.textColor);
    }
}
