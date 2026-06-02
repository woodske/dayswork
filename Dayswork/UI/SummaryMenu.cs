using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class SummaryMenu : IClickableMenu
{
    private const int MenuWidth = 760;
    private const int MaxMenuHeight = 760;
    private const int MinMenuHeight = 620;
    private const int LineSpacing = 34;
    private const int CategoryRowHeight = 40;
    private const int ArrowSize = 36;

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onConfirm;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<ContractDraft, int> _onCycleTier;
    private readonly Action<ContractDraft, TaskCategory, int> _onMoveCategory;
    private readonly SummaryReviewModel _reviewModel;

    private readonly List<string> _bodyLines = new();
    private Rectangle _bodyRect;
    private int _bodyScrollIndex;
    private int _maxBodyScrollIndex;
    private bool _draggingBodyScrollBar;
    private int _bodyScrollDragOffset;

    private ClickableComponent _tierPrevBtn = null!;
    private ClickableComponent _tierNextBtn = null!;
    private readonly List<CategoryRow> _categoryRows = new();

    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _backBtn = null!;

    public SummaryMenu(
        ContractDraft draft,
        Action<ContractDraft> onConfirm,
        Action<ContractDraft> onBack,
        Action<ContractDraft, int> onCycleTier,
        Action<ContractDraft, TaskCategory, int> onMoveCategory)
        : base(0, 0, MenuWidth, MinMenuHeight)
    {
        _draft = draft;
        _onConfirm = onConfirm;
        _onBack = onBack;
        _onCycleTier = onCycleTier;
        _onMoveCategory = onMoveCategory;
        _reviewModel = draft.PreviewState.ReviewModel;

        BuildBodyLines();
        var maxAvailableHeight = Math.Max(MinMenuHeight, Game1.uiViewport.Height - 48);
        height = Math.Min(MaxMenuHeight, maxAvailableHeight);

        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(MenuWidth, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private int ControlTop => yPositionOnScreen + 64;
    private int TierRowY => ControlTop;
    private int PriorityHeaderY => ControlTop + 52;
    private int CategoryRowsTop => ControlTop + 88;

    private void BuildComponents()
    {
        // Tier selector arrows.
        _tierPrevBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 220, TierRowY - 6, ArrowSize, ArrowSize),
            "TierPrev",
            "<") { myID = 310, rightNeighborID = 311 };
        _tierNextBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 80, TierRowY - 6, ArrowSize, ArrowSize),
            "TierNext",
            ">") { myID = 311, leftNeighborID = 310 };

        // Category priority reorder rows (up/down per row).
        _categoryRows.Clear();
        for (var i = 0; i < _reviewModel.CategoryPriority.Count; i++)
        {
            var rowY = CategoryRowsTop + i * CategoryRowHeight;
            var up = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 48, rowY, ArrowSize, ArrowSize),
                $"CatUp{i}",
                "^") { myID = 320 + i * 2 };
            var down = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 48 + ArrowSize + 6, rowY, ArrowSize, ArrowSize),
                $"CatDown{i}",
                "v") { myID = 321 + i * 2 };
            _categoryRows.Add(new CategoryRow(_reviewModel.CategoryPriority[i], i, up, down));
        }

        var btnY = yPositionOnScreen + height - 70;
        var bodyTop = CategoryRowsTop + _categoryRows.Count * CategoryRowHeight + 16;
        _bodyRect = new Rectangle(
            xPositionOnScreen + 48,
            bodyTop,
            MenuWidth - 96 - MenuScrollBar.ReservedWidth,
            btnY - bodyTop - 18);
        _maxBodyScrollIndex = Math.Max(0, _bodyLines.Count - GetVisibleBodyLineCount());
        _bodyScrollIndex = Math.Clamp(_bodyScrollIndex, 0, _maxBodyScrollIndex);

        _confirmBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + MenuWidth - 210, btnY, 170, 56),
            "Confirm",
            I18nHelper.Get("ui.summary.confirm_btn"))
        {
            myID = 300,
            leftNeighborID = 301,
        };

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, btnY, 170, 56),
            "Back",
            I18nHelper.Get("ui.summary.back_btn"))
        {
            myID = 301,
            rightNeighborID = 300,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_tierPrevBtn.bounds.Contains(x, y))
        {
            _onCycleTier(_draft, -1);
            return;
        }

        if (_tierNextBtn.bounds.Contains(x, y))
        {
            _onCycleTier(_draft, 1);
            return;
        }

        foreach (var row in _categoryRows)
        {
            if (row.Index > 0 && row.Up.bounds.Contains(x, y))
            {
                _onMoveCategory(_draft, row.Category, -1);
                return;
            }

            if (row.Index < _categoryRows.Count - 1 && row.Down.bounds.Contains(x, y))
            {
                _onMoveCategory(_draft, row.Category, 1);
                return;
            }
        }

        if (MenuScrollBar.TryBeginDrag(_bodyRect, GetVisibleBodyLineCount(), _bodyLines.Count, _bodyScrollIndex, x, y, out _bodyScrollDragOffset))
        {
            _draggingBodyScrollBar = true;
            return;
        }

        if (MenuScrollBar.UpArrowContains(_bodyRect, _bodyLines.Count, GetVisibleBodyLineCount(), x, y))
        {
            ScrollBody(-1);
            return;
        }

        if (MenuScrollBar.DownArrowContains(_bodyRect, _bodyLines.Count, GetVisibleBodyLineCount(), x, y))
        {
            ScrollBody(1);
            return;
        }

        if (MenuScrollBar.TrackContains(_bodyRect, GetVisibleBodyLineCount(), _bodyLines.Count, x, y))
        {
            _bodyScrollIndex = MenuScrollBar.GetTrackClickScrollIndex(
                _bodyRect,
                GetVisibleBodyLineCount(),
                _bodyLines.Count,
                _bodyScrollIndex,
                y);
            return;
        }

        if (_confirmBtn.bounds.Contains(x, y) && _reviewModel.CanConfirm)
        {
            _onConfirm(_draft);
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!_draggingBodyScrollBar)
            return;

        _bodyScrollIndex = MenuScrollBar.GetDragScrollIndex(
            _bodyRect,
            GetVisibleBodyLineCount(),
            _bodyLines.Count,
            _bodyScrollDragOffset,
            y);
    }

    public override void releaseLeftClick(int x, int y)
    {
        _draggingBodyScrollBar = false;
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
        if (direction == 0)
            return;

        ScrollBody(direction > 0 ? -1 : 1);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.Add(_confirmBtn);
        allClickableComponents.Add(_backBtn);
        allClickableComponents.Add(_tierPrevBtn);
        allClickableComponents.Add(_tierNextBtn);
        foreach (var row in _categoryRows)
        {
            allClickableComponents.Add(row.Up);
            allClickableComponents.Add(row.Down);
        }
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
            I18nHelper.Get("ui.summary.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        DrawTierSelector(b);
        DrawCategoryPriority(b);

        var lineY = _bodyRect.Y;
        var visibleLineCount = GetVisibleBodyLineCount();
        for (var i = _bodyScrollIndex; i < _bodyLines.Count && i < _bodyScrollIndex + visibleLineCount; i++)
        {
            Utility.drawTextWithShadow(b, _bodyLines[i], Game1.smallFont, new Vector2(_bodyRect.X, lineY), Game1.textColor);
            lineY += LineSpacing;
        }

        MenuScrollBar.Draw(b, _bodyRect, visibleLineCount, _bodyLines.Count, _bodyScrollIndex);

        DrawButton(b, _confirmBtn, _reviewModel.CanConfirm);
        DrawButton(b, _backBtn, true);
        DrawArrow(b, _tierPrevBtn, true);
        DrawArrow(b, _tierNextBtn, true);
        foreach (var row in _categoryRows)
        {
            DrawArrow(b, row.Up, row.Index > 0);
            DrawArrow(b, row.Down, row.Index < _categoryRows.Count - 1);
        }

        drawMouse(b);
    }

    private void DrawTierSelector(SpriteBatch b)
    {
        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.summary.energy_tier_label"),
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, TierRowY),
            Game1.textColor);

        var tierName = I18nHelper.Get($"ui.summary.tier.{TierKey(_reviewModel.Tier)}");
        var tierText = _reviewModel.Pricing is not null && _reviewModel.WorkerEnergy is not null
            ? I18nHelper.Get("ui.summary.tier_value", new
            {
                tier = tierName,
                energy = _reviewModel.WorkerEnergy.DailyCapacity,
                price = _reviewModel.Pricing.TotalPrice,
            })
            : tierName;

        Utility.drawTextWithShadow(
            b,
            tierText,
            Game1.smallFont,
            new Vector2(_tierPrevBtn.bounds.Right + 12, TierRowY),
            Game1.textColor);
    }

    private void DrawCategoryPriority(SpriteBatch b)
    {
        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.summary.priority_header"),
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, PriorityHeaderY),
            Game1.textColor);

        foreach (var row in _categoryRows)
        {
            var label = I18nHelper.Get($"ui.summary.category.{CategoryKey(row.Category)}");
            Utility.drawTextWithShadow(
                b,
                $"{row.Index + 1}. {label}",
                Game1.smallFont,
                new Vector2(row.Down.bounds.Right + 16, row.Up.bounds.Y + 6),
                Game1.textColor);
        }
    }

    private void BuildBodyLines()
    {
        _bodyLines.Clear();

        var tasks = _reviewModel.SelectedTasks.Count > 0
            ? string.Join(", ", _reviewModel.SelectedTasks.Select(task => I18nHelper.Get(TaskPresentation.GetI18nKey(task))))
            : I18nHelper.Get("ui.common.none");

        AddWrappedLine(I18nHelper.Get("ui.summary.tasks_label", new { tasks }));
        AddWrappedLine(I18nHelper.Get("ui.summary.outdoor_scope_label", new { count = _reviewModel.ScopeSummary.OutdoorZones.Count }));
        AddWrappedLine(I18nHelper.Get("ui.summary.animal_scope_label", new { count = _reviewModel.ScopeSummary.AnimalBuildings.Count }));
        AddWrappedLine(
            _reviewModel.ScopeSummary.Greenhouses.Count == 0
                ? I18nHelper.Get("ui.summary.greenhouse_scope_none")
                : I18nHelper.Get(
                    "ui.summary.greenhouse_scope_selected",
                    new
                    {
                        location = string.Join(
                            ", ",
                            _reviewModel.ScopeSummary.Greenhouses
                                .Select(greenhouse => greenhouse.LocationName)
                                .OrderBy(name => name, StringComparer.Ordinal)),
                    }));

        if (_reviewModel.CanConfirm && _reviewModel.Pricing is not null && _reviewModel.WorkerEnergy is not null)
        {
            AddWrappedLine(I18nHelper.Get("ui.summary.price_total", new { amount = _reviewModel.Pricing.TotalPrice }));
            AddWrappedLine(I18nHelper.Get("ui.summary.worker_energy", new { capacity = _reviewModel.WorkerEnergy.DailyCapacity }));
            AddWrappedLine(GetPaymentTimingText(_reviewModel.PaymentTimingKind));
        }
        else
        {
            AddWrappedLine(I18nHelper.Get("ui.summary.validation_header"));
            foreach (var message in _reviewModel.ValidationMessages
                         .GroupBy(candidate => candidate.Code)
                         .Select(group => group.First()))
                AddWrappedLine(BuildValidationText(message));
        }
    }

    private string BuildValidationText(ValidationDisplayMessage message)
    {
        return message.Code switch
        {
            ContractValidationCode.NoChargeableScopeTaskPair => I18nHelper.Get("ui.summary.validation.no_chargeable"),
            ContractValidationCode.NoAnimalBuildingForSelectedAnimalService =>
                I18nHelper.Get("ui.summary.validation.no_animal"),
            ContractValidationCode.NoGreenhouseScopeForSelectedGreenhouseService =>
                I18nHelper.Get("ui.summary.validation.no_greenhouse"),
            _ => I18nHelper.Get("ui.summary.validation.no_outdoor"),
        };
    }

    private string GetPaymentTimingText(PaymentTimingKind kind) => kind switch
    {
        PaymentTimingKind.RecurringStartsNextEligibleDay => I18nHelper.Get("ui.summary.payment_timing_recurring"),
        PaymentTimingKind.RecurringEditAppliesNextEligibleDay => I18nHelper.Get("ui.summary.payment_timing_recurring_edit"),
        _ => I18nHelper.Get("ui.summary.payment_timing_one_time"),
    };

    private static string TierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
        _ => tier.ToString(),
    };

    private static string CategoryKey(TaskCategory category) => category switch
    {
        TaskCategory.AnimalCare => "animal_care",
        TaskCategory.Crops => "crops",
        TaskCategory.Fieldwork => "fieldwork",
        _ => category.ToString(),
    };

    private void AddWrappedLine(string value)
    {
        var wrapped = Game1.parseText(value, Game1.smallFont, MenuWidth - 96 - MenuScrollBar.ReservedWidth)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        foreach (var line in wrapped.Split('\n'))
        {
            _bodyLines.Add(line);
        }
    }

    private int GetVisibleBodyLineCount() => Math.Max(1, _bodyRect.Height / LineSpacing);

    private void ScrollBody(int delta)
    {
        var next = Math.Clamp(_bodyScrollIndex + delta, 0, _maxBodyScrollIndex);
        if (next == _bodyScrollIndex)
            return;

        _bodyScrollIndex = next;
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
                btn.bounds.X + (int)(btn.bounds.Width - textSize.X) / 2,
                btn.bounds.Y + (int)(btn.bounds.Height - textSize.Y) / 2),
            textTint);
    }

    private static void DrawArrow(SpriteBatch b, ClickableComponent btn, bool enabled)
    {
        var tint = enabled ? Color.White : Color.Gray * 0.5f;
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width - textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - textSize.Y) / 2),
            textTint);
    }

    private sealed record CategoryRow(
        TaskCategory Category,
        int Index,
        ClickableComponent Up,
        ClickableComponent Down);
}
