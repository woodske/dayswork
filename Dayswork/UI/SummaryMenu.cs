using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Screen 4 (thin) of the hiring flow — confirms the contract (M-07).
// _estimatedHours, _rate, and _depositAmount are all computed in the
// constructor so draw() has zero Core interface calls (NFR-PERF-01).
internal sealed class SummaryMenu : IClickableMenu
{
    private const int MenuWidth     = 700;
    private const int MinMenuHeight = 500;

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft, int, int> _onConfirm;
    private readonly Action<ContractDraft> _onBack;

    // Pre-computed in ctor; read-only in draw()
    private readonly double _estimatedHours;
    private readonly int    _rate;
    private readonly int    _depositAmount;
    private readonly string _tasksLabel;          // flat comma-separated list
    private readonly string _wrappedTasksList;    // word-wrapped for display
    private readonly int    _wrappedTasksHeight;  // cached to avoid MeasureString in draw()

    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _backBtn    = null!;

    public SummaryMenu(
        ContractDraft draft,
        IDepositCalculator depositCalc,
        IConfigSnapshot config,
        Zone wholeFarmFallback,
        Action<ContractDraft, int, int> onConfirm,
        Action<ContractDraft> onBack)
        : base(0, 0, MenuWidth, MinMenuHeight)
    {
        _draft     = draft;
        _onConfirm = onConfirm;
        _onBack    = onBack;

        // Compute all display values once here (NFR-PERF-01, NFR-PERF-02)

        _estimatedHours = DepositHoursPolicy.EstimateBillableHours(
            draft.Zones.Count > 0 ? draft.Zones : new[] { wholeFarmFallback },
            draft.EnabledTasks.Count,
            config);
        // isRaining: false at hire time; rain branch applies at shift start
        _rate = new Core.Pricing.RateCalculator().Calculate(draft.EnabledTasks, config, isRaining: false);

        _depositAmount = depositCalc.Calculate(_estimatedHours, _rate) switch
        {
            PositiveDeposit pd => pd.Amount,
            _                  => 0,
        };

        _tasksLabel = draft.EnabledTasks.Count > 0
            ? string.Join(", ", draft.EnabledTasks.Select(t => I18nHelper.Get($"ui.task_selection.{ToKey(t)}")))
            : "—";

        _wrappedTasksList   = Game1.parseText(_tasksLabel, Game1.smallFont, MenuWidth - 96);
        _wrappedTasksHeight = (int)Game1.smallFont.MeasureString(_wrappedTasksList).Y;

        // Expand height to fit content so buttons never overlap text.
        // Mirrors draw() layout: 90px top pad + task header + wrapped list + 3 data lines +
        // spacer + refund line, then 20px gap before the 70px button strip at the bottom.
        const int lineSpacing = 48;
        int contentHeight = 90 + lineSpacing + (_wrappedTasksHeight + 8)
                          + lineSpacing + lineSpacing + lineSpacing
                          + 12 + lineSpacing;
        height = Math.Max(MinMenuHeight, contentHeight + 20 + 70);

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        int btnY = yPositionOnScreen + height - 70;

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Confirm", I18nHelper.Get("ui.summary.confirm_btn"))
        {
            myID           = 300,
            leftNeighborID = 301,
        };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back", I18nHelper.Get("ui.summary.back_btn"))
        {
            myID            = 301,
            rightNeighborID = 300,
        };
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_confirmBtn.bounds.Contains(x, y)) { _onConfirm(_draft, _depositAmount, _rate); return; }
        if (_backBtn.bounds.Contains(x, y))    { _onBack(_draft);                           return; }
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
        allClickableComponents.Add(_confirmBtn);
        allClickableComponents.Add(_backBtn);
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
            b,
            I18nHelper.Get("ui.summary.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        int lineX = xPositionOnScreen + 48;
        int lineY = yPositionOnScreen + 90;
        const int lineSpacing = 48;

        // "Tasks:" header on its own line, then the wrapped list below it
        DrawLine(b, I18nHelper.Get("ui.summary.tasks_label"), ref lineY, lineX, lineSpacing);
        Utility.drawTextWithShadow(b, _wrappedTasksList, Game1.smallFont,
            new Vector2(lineX + 8, lineY), Game1.textColor);
        lineY += _wrappedTasksHeight + 8;

        DrawLine(b, I18nHelper.Get("ui.summary.hours_label",   new { hours   = _estimatedHours.ToString("F1") }), ref lineY, lineX, lineSpacing);
        DrawLine(b, I18nHelper.Get("ui.summary.rate_label",    new { rate    = _rate }),                           ref lineY, lineX, lineSpacing);
        DrawLine(b, I18nHelper.Get("ui.summary.deposit_label", new { deposit = _depositAmount }),                  ref lineY, lineX, lineSpacing);
        lineY += 12;
        DrawLine(b, I18nHelper.Get("ui.summary.refund_policy"), ref lineY, lineX, lineSpacing);

        DrawButton(b, _confirmBtn);
        DrawButton(b, _backBtn);

        drawMouse(b);
    }

    private static void DrawLine(SpriteBatch b, string text, ref int y, int x, int spacing)
    {
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x, y), Game1.textColor);
        y += spacing;
    }

    private static void DrawButton(SpriteBatch b, ClickableComponent btn)
    {
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(b, btn.label, Game1.smallFont,
            new Vector2(
                btn.bounds.X + (int)(btn.bounds.Width  - textSize.X) / 2,
                btn.bounds.Y + (int)(btn.bounds.Height - textSize.Y) / 2),
            Game1.textColor);
    }

    // Maps TaskKind enum name to i18n key suffix (mirrors TaskI18nKeys in TaskSelectionMenu)
    private static string ToKey(TaskKind task) => task switch
    {
        TaskKind.WaterCrops           => "water_crops",
        TaskKind.HarvestCrops         => "harvest_crops",
        TaskKind.CollectFruit         => "collect_fruit",
        TaskKind.FeedAnimals          => "feed_animals",
        TaskKind.PetAnimals           => "pet_animals",
        TaskKind.CollectAnimalProducts => "collect_animal_products",
        TaskKind.CutTrees             => "cut_trees",
        TaskKind.ClearRocks           => "clear_rocks",
        TaskKind.ClearWeeds           => "clear_weeds",
        TaskKind.ClearGrass           => "clear_grass",
        _                             => task.ToString().ToLower(),
    };
}
