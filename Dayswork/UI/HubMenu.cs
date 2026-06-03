using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Landing page for the contract flow. Instead of a linear wizard, the player picks any section
// from here; each section page returns to this hub. The final Hire/Confirm action lives here and is
// gated on the same validity check the coordinator enforces. draw() reads only pre-computed state.
internal sealed class HubMenu : IClickableMenu
{
    private const int RowHeight = 60;
    private const int RowGap = 12;

    private static readonly Color DoneColor = new(34, 139, 34);
    private static readonly Color NeedsSetupColor = new(178, 34, 34);
    private static readonly Color NeutralColor = new(96, 72, 48);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onConfirm;
    private readonly Action _onCancel;
    private readonly List<NavItem> _items = new();

    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _cancelBtn = null!;

    public HubMenu(
        ContractDraft draft,
        Action<ContractDraft> onTaskSelection,
        Action<ContractDraft> onWorkScope,
        Action<ContractDraft> onOutput,
        Action<ContractDraft> onPriority,
        Action<ContractDraft> onEnergy,
        Action<ContractDraft> onRecurrence,
        Action<ContractDraft> onSummary,
        Action<ContractDraft> onConfirm,
        Action onCancel)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _draft = draft;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        _items.Add(new NavItem("ui.hub.task_selection", onTaskSelection, TaskSelectionStatus));
        _items.Add(new NavItem("ui.hub.work_scope", onWorkScope, WorkScopeStatus));
        _items.Add(new NavItem("ui.hub.output_destination", onOutput, OutputStatus));
        _items.Add(new NavItem("ui.hub.task_priority", onPriority, () => HubStatus.None));
        _items.Add(new NavItem("ui.hub.energy", onEnergy, EnergyStatus));
        _items.Add(new NavItem("ui.hub.recurrence", onRecurrence, RecurrenceStatus));
        _items.Add(new NavItem("ui.hub.summary", onSummary, () => HubStatus.None));

        BuildComponents();
        populateClickableComponentList();
    }

    private bool CanConfirm => _draft.PreviewState.ReviewModel.CanConfirm;

    private void BuildComponents()
    {
        var rowX = xPositionOnScreen + 48;
        var rowW = width - 96;
        var startY = yPositionOnScreen + 80;

        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Button = new ClickableComponent(
                new Rectangle(rowX, startY + i * (RowHeight + RowGap), rowW, RowHeight),
                _items[i].LabelKey,
                I18nHelper.Get(_items[i].LabelKey))
            {
                myID = 400 + i,
                upNeighborID = i > 0 ? 400 + i - 1 : -1,
                downNeighborID = i < _items.Count - 1 ? 400 + i + 1 : 411,
            };
        }

        var btnY = yPositionOnScreen + height - 70;

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + width - 210, btnY, 170, 56),
            "Confirm",
            I18nHelper.Get("ui.hub.confirm_btn"))
        {
            myID = 410,
            upNeighborID = 400 + _items.Count - 1,
            leftNeighborID = 411,
        };

        _cancelBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Cancel",
            I18nHelper.Get("ui.hub.cancel_btn"))
        {
            myID = 411,
            upNeighborID = 400 + _items.Count - 1,
            rightNeighborID = 410,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var item in _items)
        {
            if (!item.Button.bounds.Contains(x, y))
                continue;

            Game1.playSound("smallSelect");
            item.Open(_draft);
            return;
        }

        if (_confirmBtn.bounds.Contains(x, y) && CanConfirm)
        {
            Game1.playSound("smallSelect");
            _onConfirm(_draft);
            return;
        }

        if (_cancelBtn.bounds.Contains(x, y))
        {
            Game1.playSound("bigDeSelect");
            _onCancel();
        }
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
        foreach (var item in _items)
            allClickableComponents.Add(item.Button);
        allClickableComponents.Add(_confirmBtn);
        allClickableComponents.Add(_cancelBtn);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _items.Count > 0 ? _items[0].Button : _confirmBtn;
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
            I18nHelper.Get("ui.hub.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        foreach (var item in _items)
        {
            var btn = item.Button;
            drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);

            Utility.drawTextWithShadow(
                b,
                btn.label,
                Game1.smallFont,
                new Vector2(btn.bounds.X + 20, btn.bounds.Y + (btn.bounds.Height - (int)Game1.smallFont.MeasureString(btn.label).Y) / 2),
                Game1.textColor);

            DrawStatus(b, btn, item.Status());
        }

        DrawButton(b, _confirmBtn, CanConfirm);
        DrawButton(b, _cancelBtn, true);

        drawMouse(b);
    }

    private static void DrawStatus(SpriteBatch b, ClickableComponent btn, HubStatus status)
    {
        if (string.IsNullOrEmpty(status.Text))
            return;

        var size = Game1.smallFont.MeasureString(status.Text);
        Utility.drawTextWithShadow(
            b,
            status.Text,
            Game1.smallFont,
            new Vector2(btn.bounds.Right - 20 - size.X, btn.bounds.Y + (btn.bounds.Height - (int)size.Y) / 2),
            status.Color);
    }

    private HubStatus TaskSelectionStatus() =>
        _draft.EnabledTasks.Count > 0 ? Done() : NeedsSetup();

    private HubStatus WorkScopeStatus() =>
        _draft.OutdoorZones.Count > 0 || _draft.AnimalBuildings.Count > 0 || _draft.Greenhouses.Count > 0
            ? Done()
            : NeedsSetup();

    private HubStatus OutputStatus() =>
        _draft.Destinations.Count > 0 ? Done() : Optional();

    // Energy/Recurrence always have a value; show the current selection as the status text.
    private HubStatus EnergyStatus() =>
        new(I18nHelper.Get($"ui.summary.tier.{TierKey(_draft.Tier)}"), NeutralColor);

    private HubStatus RecurrenceStatus() =>
        new(I18nHelper.Get(_draft.Schedule == ContractSchedule.OneTime ? "ui.schedule.one_time" : "ui.schedule.recurring"), NeutralColor);

    private static HubStatus Done() => new(I18nHelper.Get("ui.hub.status_done"), DoneColor);
    private static HubStatus NeedsSetup() => new(I18nHelper.Get("ui.hub.status_needs_setup"), NeedsSetupColor);
    private static HubStatus Optional() => new(I18nHelper.Get("ui.hub.status_optional"), NeutralColor);

    private static string TierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
        _ => tier.ToString(),
    };

    private readonly record struct HubStatus(string Text, Color Color)
    {
        public static readonly HubStatus None = new(string.Empty, default);
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

    private sealed class NavItem
    {
        public NavItem(string labelKey, Action<ContractDraft> open, Func<HubStatus> status)
        {
            LabelKey = labelKey;
            Open = open;
            Status = status;
        }

        public string LabelKey { get; }
        public Action<ContractDraft> Open { get; }
        public Func<HubStatus> Status { get; }
        public ClickableComponent Button { get; set; } = null!;
    }
}
