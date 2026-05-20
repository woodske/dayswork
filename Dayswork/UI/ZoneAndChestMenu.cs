using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Screen 2 of the hiring flow — zone summary and output chest assignment (M-05).
// Zone drawing happens in ZoneDrawMenu (Robin UX: player warps to farm).
// All game-state queries happen in the constructor or on explicit user actions.
// draw() reads only pre-cached fields (NFR-PERF-01).
internal sealed class ZoneAndChestMenu : IClickableMenu
{
    private const int MenuWidth  = 700;
    private const int MenuHeight = 700;

    // Tasks that produce items needing an output destination.
    // ClearGrass excluded: hay → silo/ground, never a chest (FR-TASK-09).
    private static readonly TaskKind[] OutputTasks =
    {
        TaskKind.HarvestCrops, TaskKind.CollectFruit, TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees, TaskKind.ClearRocks, TaskKind.ClearWeeds,
    };

    // These three can also target the Shipping Bin (FR-TASK-02)
    private static readonly HashSet<TaskKind> ShippingBinEligible = new()
    {
        TaskKind.HarvestCrops, TaskKind.CollectFruit, TaskKind.CollectAnimalProducts,
    };

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onAdvance;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<ContractDraft> _onBeginZoneDraw;

    // Pre-computed on open (NFR-PERF-01)
    private readonly List<ChestEntry> _chestList;
    private readonly TaskKind[]       _enabledOutputTasks;

    // Output assignment state
    private readonly Dictionary<TaskKind, DestinationKey> _outputAssignments = new();
    private bool          _showingPicker;
    private TaskKind      _pickerTask;
    private List<(string Label, DestinationKey Dest)> _pickerOptions = new();
    private Rectangle     _pickerPanelRect;
    private List<ClickableComponent> _pickerRows = new();

    // UI components
    private ClickableComponent _workAreaBtn   = null!;
    private ClickableComponent _clearZonesBtn = null!;
    private ClickableComponent _confirmBtn    = null!;
    private ClickableComponent _backBtn       = null!;
    private readonly List<ClickableComponent> _setOutputBtns = new();

    public ZoneAndChestMenu(
        ContractDraft draft,
        ChestResolver chestResolver,
        Action<ContractDraft> onAdvance,
        Action<ContractDraft> onBack,
        Action<ContractDraft> onBeginZoneDraw)
        : base(0, 0, MenuWidth, MenuHeight)
    {
        _draft           = draft;
        _onAdvance       = onAdvance;
        _onBack          = onBack;
        _onBeginZoneDraw = onBeginZoneDraw;

        // Pre-compute game-state queries once on open (NFR-PERF-01)
        var farm  = Game1.getFarm();
        _chestList = chestResolver.GetAllChests(farm);

        // Which enabled tasks produce assignable output?
        _enabledOutputTasks = OutputTasks.Where(t => draft.EnabledTasks.Contains(t)).ToArray();

        // Restore output assignments from draft
        foreach (var (task, dest) in draft.Destinations)
            _outputAssignments[task] = dest;

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, MenuHeight);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    // ── Component construction ───────────────────────────────────────────────

    private void BuildComponents()
    {
        int x0    = xPositionOnScreen + 48;
        int zoneY = yPositionOnScreen + 70;

        // Size zone buttons to fit their labels with even padding.
        string workAreaLabel = I18nHelper.Get("ui.zone_chest.select_work_area_btn");
        string clearLabel    = I18nHelper.Get("ui.zone_chest.clear_zones_btn");
        int btnW = (int)Math.Ceiling(Math.Max(
            Game1.smallFont.MeasureString(workAreaLabel).X,
            Game1.smallFont.MeasureString(clearLabel).X)) + 56;
        int btnH = 56;

        _workAreaBtn = new ClickableComponent(
            new Rectangle(x0, zoneY, btnW, btnH),
            "WorkArea", workAreaLabel)
        { myID = 100, downNeighborID = 101 };

        _clearZonesBtn = new ClickableComponent(
            new Rectangle(x0, zoneY + btnH + 8, btnW, btnH),
            "ClearZones", clearLabel)
        { myID = 101, upNeighborID = 100, downNeighborID = 200 };

        // "Set" button per enabled output task — sized to contain its label.
        _setOutputBtns.Clear();
        string setLabel = I18nHelper.Get("ui.zone_chest.set_output_btn");
        int setBtnW = (int)Math.Ceiling(Game1.smallFont.MeasureString(setLabel).X) + 40;
        int setBtnH = 48;
        const int rowH = 52;
        int outY    = yPositionOnScreen + 310;
        for (int i = 0; i < _enabledOutputTasks.Length; i++)
        {
            int btnTop = outY + i * rowH + (rowH - setBtnH) / 2;
            _setOutputBtns.Add(new ClickableComponent(
                new Rectangle(xPositionOnScreen + MenuWidth - setBtnW - 32, btnTop, setBtnW, setBtnH),
                _enabledOutputTasks[i].ToString(),
                setLabel)
            {
                myID           = 200 + i,
                upNeighborID   = i > 0 ? 199 + i : 101,
                downNeighborID = i < _enabledOutputTasks.Length - 1 ? 201 + i : 900,
            });
        }

        int btnY      = yPositionOnScreen + MenuHeight - 70;
        int lastSetId = _enabledOutputTasks.Length > 0 ? 200 + _enabledOutputTasks.Length - 1 : 101;

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Next", I18nHelper.Get("ui.zone_chest.confirm_btn"))
        { myID = 900, upNeighborID = lastSetId, leftNeighborID = 901 };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back", I18nHelper.Get("ui.zone_chest.back_btn"))
        { myID = 901, upNeighborID = lastSetId, rightNeighborID = 900 };
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // Picker popup: intercept all clicks while open
        if (_showingPicker)
        {
            foreach (var row in _pickerRows)
            {
                if (!row.bounds.Contains(x, y)) continue;
                int idx = _pickerRows.IndexOf(row);
                ApplyPickerSelection(_pickerOptions[idx].Dest);
                Game1.playSound("smallSelect");
                return;
            }
            _showingPicker = false;
            return;
        }

        if (_workAreaBtn.bounds.Contains(x, y))
        {
            Game1.playSound("smallSelect");
            _onBeginZoneDraw(_draft);
            return;
        }
        if (_clearZonesBtn.bounds.Contains(x, y))
        {
            _draft.Zones.Clear();
            Game1.playSound("trashcan");
            return;
        }
        for (int i = 0; i < _setOutputBtns.Count; i++)
        {
            if (!_setOutputBtns[i].bounds.Contains(x, y)) continue;
            OpenPicker(_enabledOutputTasks[i], _setOutputBtns[i].bounds);
            Game1.playSound("smallSelect");
            return;
        }
        if (_confirmBtn.bounds.Contains(x, y)) { ConfirmAndAdvance(); return; }
        if (_backBtn.bounds.Contains(x, y))    { _onBack(_draft);     return; }
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

    // ── Output assignment ────────────────────────────────────────────────────

    private void OpenPicker(TaskKind task, Rectangle setButtonBounds)
    {
        _pickerTask    = task;
        _showingPicker = true;

        _pickerOptions = new List<(string, DestinationKey)>();
        _pickerOptions.Add((I18nHelper.Get("ui.zone_chest.picker_mail_option"), MailDestination.Instance));

        if (ShippingBinEligible.Contains(task))
            _pickerOptions.Add((I18nHelper.Get("ui.zone_chest.shipping_bin_option"), ShippingBinDestination.Instance));

        foreach (var entry in _chestList)
            _pickerOptions.Add((entry.DisplayName, new ChestDestination(entry.Ref)));

        const int rowH = 44;
        // Size panel to fit the widest label, capped at the menu interior width.
        float maxLabelW = _pickerOptions.Max(o => Game1.smallFont.MeasureString(o.Label).X);
        int   panelW    = Math.Min((int)Math.Ceiling(maxLabelW) + 32, MenuWidth - 16);
        int   panelH    = _pickerOptions.Count * rowH + 16;
        // Open to the left of the Set button so it stays inside the menu.
        int   panelX    = Math.Max(xPositionOnScreen + 8, setButtonBounds.X - panelW - 8);
        int   panelY    = Math.Max(yPositionOnScreen + 8, setButtonBounds.Y - panelH / 2);
        panelY = Math.Max(yPositionOnScreen + 8, Math.Min(panelY, yPositionOnScreen + MenuHeight - panelH - 8));

        _pickerPanelRect = new Rectangle(panelX, panelY, panelW, panelH);

        _pickerRows = new List<ClickableComponent>();
        for (int i = 0; i < _pickerOptions.Count; i++)
        {
            _pickerRows.Add(new ClickableComponent(
                new Rectangle(panelX + 8, panelY + 8 + i * rowH, panelW - 16, rowH - 4),
                i.ToString(), _pickerOptions[i].Label)
            { myID = 600 + i });
        }
    }

    private void ApplyPickerSelection(DestinationKey dest)
    {
        _outputAssignments[_pickerTask] = dest;
        _draft.Destinations[_pickerTask] = dest;
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

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.Add(_workAreaBtn);
        allClickableComponents.Add(_clearZonesBtn);
        allClickableComponents.AddRange(_setOutputBtns);
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

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        // Title
        Utility.drawTextWithShadow(b, I18nHelper.Get("ui.zone_chest.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        // Zone section
        string zoneLabel = _draft.Zones.Count > 0
            ? I18nHelper.Get("ui.zone_chest.zone_count_label", new { count = _draft.Zones.Count })
            : I18nHelper.Get("ui.zone_chest.no_zones_hint");
        Utility.drawTextWithShadow(b, zoneLabel, Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, yPositionOnScreen + 205),
            Game1.textColor);

        DrawButton(b, _workAreaBtn,   enabled: true);
        DrawButton(b, _clearZonesBtn, enabled: _draft.Zones.Count > 0);

        // Separator
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 32, yPositionOnScreen + 245, MenuWidth - 64, 2),
            Color.Gray * 0.4f);

        // Output section
        Utility.drawTextWithShadow(b, I18nHelper.Get("ui.zone_chest.output_section_label"),
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, yPositionOnScreen + 260),
            Game1.textColor);

        if (_enabledOutputTasks.Length == 0)
        {
            Utility.drawTextWithShadow(b, I18nHelper.Get("ui.zone_chest.no_output_tasks"),
                Game1.smallFont,
                new Vector2(xPositionOnScreen + 48, yPositionOnScreen + 310),
                Color.Gray);
        }
        else
        {
            const int rowH = 52;
            int outY = yPositionOnScreen + 310;
            for (int i = 0; i < _enabledOutputTasks.Length; i++)
            {
                var task     = _enabledOutputTasks[i];
                var taskName = I18nHelper.Get($"ui.task_selection.{ToKey(task)}");
                var destName = GetDestinationLabel(task);
                Utility.drawTextWithShadow(b, $"{taskName}:", Game1.smallFont,
                    new Vector2(xPositionOnScreen + 48, outY + i * rowH + 6), Game1.textColor);
                Utility.drawTextWithShadow(b, destName, Game1.smallFont,
                    new Vector2(xPositionOnScreen + 48, outY + i * rowH + 26), Color.DimGray);
                DrawButton(b, _setOutputBtns[i], enabled: true);
            }
        }

        DrawButton(b, _confirmBtn, enabled: true);
        DrawButton(b, _backBtn,    enabled: true);

        if (_showingPicker)
            DrawPicker(b);

        drawMouse(b);
    }

    private void DrawPicker(SpriteBatch b)
    {
        drawTextureBox(b, _pickerPanelRect.X - 4, _pickerPanelRect.Y - 4,
            _pickerPanelRect.Width + 8, _pickerPanelRect.Height + 8, Color.White);

        for (int i = 0; i < _pickerRows.Count; i++)
        {
            var row  = _pickerRows[i];
            var dest = _pickerOptions[i].Dest;

            bool selected = _outputAssignments.TryGetValue(_pickerTask, out var cur) &&
                            EqualityComparer<DestinationKey>.Default.Equals(cur, dest);
            if (selected)
                b.Draw(Game1.staminaRect, row.bounds, Color.LightBlue * 0.5f);

            Utility.drawTextWithShadow(b, row.label, Game1.smallFont,
                new Vector2(row.bounds.X + 6, row.bounds.Y + (row.bounds.Height - (int)Game1.smallFont.MeasureString("A").Y) / 2),
                selected ? Color.DarkBlue : Game1.textColor);
        }
    }

    // ── Draw helpers ─────────────────────────────────────────────────────────

    private static void DrawButton(SpriteBatch b, ClickableComponent btn, bool enabled)
    {
        var tint     = enabled ? Color.White : Color.Gray;
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(b, btn.label, Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width  - (int)textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)textSize.Y) / 2),
            textTint);
    }

    private string GetDestinationLabel(TaskKind task)
    {
        if (!_outputAssignments.TryGetValue(task, out var dest))
            return I18nHelper.Get("ui.zone_chest.no_chest_assigned");
        return dest switch
        {
            ShippingBinDestination => I18nHelper.Get("ui.zone_chest.shipping_bin_option"),
            ChestDestination cd    => _chestList.FirstOrDefault(e => e.Ref == cd.Ref)?.DisplayName
                                      ?? cd.Ref.ToString(),
            _                      => I18nHelper.Get("ui.zone_chest.no_chest_assigned"),
        };
    }

    private static string ToKey(TaskKind task) => task switch
    {
        TaskKind.WaterCrops            => "water_crops",
        TaskKind.HarvestCrops          => "harvest_crops",
        TaskKind.CollectFruit          => "collect_fruit",
        TaskKind.FeedAnimals           => "feed_animals",
        TaskKind.PetAnimals            => "pet_animals",
        TaskKind.CollectAnimalProducts => "collect_animal_products",
        TaskKind.CutTrees              => "cut_trees",
        TaskKind.ClearRocks            => "clear_rocks",
        TaskKind.ClearWeeds            => "clear_weeds",
        TaskKind.ClearGrass            => "clear_grass",
        _                              => task.ToString().ToLowerInvariant(),
    };
}
