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

// Screen 1 of the hiring flow — task toggles + live hourly rate (M-04).
// All Core interface calls happen in the constructor and toggle handler;
// draw() reads only pre-computed fields (NFR-PERF-01).
internal sealed class TaskSelectionMenu : IClickableMenu
{
    private const int MenuWidth  = 700;
    private const int MenuHeight = 700;
    private const int RowHeight  = 50;

    // FR-WORK-03 task display order (mirrors execution priority so the player
    // sees tasks in the order the worker will do them)
    private static readonly TaskKind[] TaskOrder = new TaskKind[]
    {
        TaskKind.WaterCrops, TaskKind.HarvestCrops, TaskKind.CollectFruit,
        TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees, TaskKind.ClearRocks, TaskKind.ClearWeeds, TaskKind.ClearGrass,
    };

    private static readonly Dictionary<TaskKind, string> TaskI18nKeys = new()
    {
        [TaskKind.WaterCrops]            = "ui.task_selection.water_crops",
        [TaskKind.HarvestCrops]          = "ui.task_selection.harvest_crops",
        [TaskKind.CollectFruit]          = "ui.task_selection.collect_fruit",
        [TaskKind.FeedAnimals]           = "ui.task_selection.feed_animals",
        [TaskKind.PetAnimals]            = "ui.task_selection.pet_animals",
        [TaskKind.CollectAnimalProducts] = "ui.task_selection.collect_animal_products",
        [TaskKind.CutTrees]              = "ui.task_selection.cut_trees",
        [TaskKind.ClearRocks]            = "ui.task_selection.clear_rocks",
        [TaskKind.ClearWeeds]            = "ui.task_selection.clear_weeds",
        [TaskKind.ClearGrass]            = "ui.task_selection.clear_grass",
    };

    private readonly ContractDraft _draft;
    private readonly IRateCalculator _rateCalc;
    private readonly IConfigSnapshot _config;
    private readonly Action<ContractDraft> _onAdvance;
    private readonly Action _onCancel;

    // Cached on each toggle; read-only in draw()
    private int _currentRate;

    private readonly List<ClickableComponent> _toggleRows = new();
    private ClickableComponent _nextBtn = null!;
    private ClickableComponent _cancelBtn = null!;

    public TaskSelectionMenu(
        ContractDraft draft,
        IRateCalculator rateCalc,
        IConfigSnapshot config,
        Action<ContractDraft> onAdvance,
        Action onCancel)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft     = draft;
        _rateCalc  = rateCalc;
        _config    = config;
        _onAdvance = onAdvance;
        _onCancel  = onCancel;

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        // Compute initial rate (draft starts with no tasks enabled → base rate only)
        _currentRate = rateCalc.Calculate(draft.EnabledTasks, config, isRaining: false);

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        _toggleRows.Clear();

        int rowX   = xPositionOnScreen + 48;
        int rowW   = MenuWidth - 96;
        int startY = yPositionOnScreen + 75;

        for (int i = 0; i < TaskOrder.Length; i++)
        {
            _toggleRows.Add(new ClickableComponent(
                new Rectangle(rowX, startY + i * RowHeight, rowW, RowHeight - 4),
                name:  TaskOrder[i].ToString(),
                label: I18nHelper.Get(TaskI18nKeys[TaskOrder[i]]))
            {
                myID           = 100 + i,
                upNeighborID   = i > 0 ? 99 + i : -1,  // -1 = IClickableMenu.SNAP_AUTOMATIC
                downNeighborID = i < TaskOrder.Length - 1 ? 101 + i : 200,
            });
        }

        int btnY = yPositionOnScreen + MenuHeight - 70;

        _nextBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next", I18nHelper.Get("ui.task_selection.confirm_btn"))
        {
            myID           = 200,
            upNeighborID   = 109,
            leftNeighborID = 201,
        };

        _cancelBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Cancel", I18nHelper.Get("ui.task_selection.cancel_btn"))
        {
            myID            = 201,
            upNeighborID    = 109,
            rightNeighborID = 200,
        };
    }

    private void ToggleTask(TaskKind task)
    {
        if (!_draft.EnabledTasks.Remove(task))
            _draft.EnabledTasks.Add(task);
        _currentRate = _rateCalc.Calculate(_draft.EnabledTasks, _config, isRaining: false);
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var row in _toggleRows)
        {
            if (!row.bounds.Contains(x, y)) continue;
            ToggleTask(Enum.Parse<TaskKind>(row.name));
            Game1.playSound("smallSelect");
            return;
        }

        if (_nextBtn.bounds.Contains(x, y) && _draft.EnabledTasks.Count > 0) { _onAdvance(_draft); return; }
        if (_cancelBtn.bounds.Contains(x, y)) { _onCancel(); return; }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { _onCancel(); return; }
        base.receiveGamePadButton(b);
    }

    // ── Gamepad snapping ─────────────────────────────────────────────────────

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
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.task_selection.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        for (int i = 0; i < _toggleRows.Count; i++)
        {
            var row = _toggleRows[i];
            bool on = _draft.EnabledTasks.Contains(TaskOrder[i]);

            drawTextureBox(b, row.bounds.X, row.bounds.Y,
                row.bounds.Width, row.bounds.Height, Color.White);

            // Vanilla checkbox sprite: unchecked=(227,425,9,9), checked=(236,425,9,9), scaled 4x to 36×36
            b.Draw(Game1.mouseCursors,
                new Rectangle(row.bounds.X + 8, row.bounds.Y + (row.bounds.Height - 36) / 2, 36, 36),
                new Rectangle(on ? 236 : 227, 425, 9, 9),
                Color.White);

            Utility.drawTextWithShadow(b, row.label, Game1.smallFont,
                new Vector2(row.bounds.X + 52, row.bounds.Y + 10),
                Game1.textColor);
        }

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.task_selection.rate_label", new { rate = _currentRate }),
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, yPositionOnScreen + MenuHeight - 110),
            Game1.textColor);

        DrawButton(b, _nextBtn,   enabled: _draft.EnabledTasks.Count > 0);
        DrawButton(b, _cancelBtn, enabled: true);

        drawMouse(b);
    }

    private static void DrawButton(SpriteBatch b, ClickableComponent btn, bool enabled)
    {
        var tint     = enabled ? Color.White : Color.Gray;
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(b, btn.label, Game1.smallFont,
            new Vector2(
                btn.bounds.X + (int)(btn.bounds.Width  - textSize.X) / 2,
                btn.bounds.Y + (int)(btn.bounds.Height - textSize.Y) / 2),
            textTint);
    }
}
