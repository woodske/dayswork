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
    private const int MaxMenuHeight = 700;
    private const int MinMenuHeight = 560;
    private const int LineSpacing = 34;

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onConfirm;
    private readonly Action<ContractDraft> _onBack;
    private readonly SummaryReviewModel _reviewModel;

    private readonly List<string> _bodyLines = new();
    private Rectangle _bodyRect;
    private int _bodyScrollIndex;
    private int _maxBodyScrollIndex;
    private bool _draggingBodyScrollBar;
    private int _bodyScrollDragOffset;

    private ClickableComponent _confirmBtn = null!;
    private ClickableComponent _backBtn = null!;

    public SummaryMenu(
        ContractDraft draft,
        Action<ContractDraft> onConfirm,
        Action<ContractDraft> onBack)
        : base(0, 0, MenuWidth, MinMenuHeight)
    {
        _draft = draft;
        _onConfirm = onConfirm;
        _onBack = onBack;
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

    private void BuildComponents()
    {
        var btnY = yPositionOnScreen + height - 70;
        _bodyRect = new Rectangle(
            xPositionOnScreen + 48,
            yPositionOnScreen + 80,
            MenuWidth - 96 - MenuScrollBar.ReservedWidth,
            btnY - (yPositionOnScreen + 80) - 18);
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
        drawMouse(b);
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
            AddWrappedLine(I18nHelper.Get("ui.summary.price_breakdown_header"));
            foreach (var lineItem in _reviewModel.Pricing.LineItems)
            {
                var lineLabel = BuildPricingLineLabel(lineItem);
                AddWrappedLine(I18nHelper.Get("ui.summary.price_line", new { label = lineLabel, amount = lineItem.LineTotal }));
            }

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

    private string BuildPricingLineLabel(PricingLineItem lineItem)
    {
        var taskLabel = I18nHelper.Get(TaskPresentation.GetI18nKey(lineItem.Service));
        return lineItem.Family switch
        {
            PricingFamily.Outdoor when lineItem.OutdoorBand is not null =>
                I18nHelper.Get(
                    "ui.summary.price_line_outdoor",
                    new { service = taskLabel, band = Humanize(lineItem.OutdoorBand.Value.ToString()) }),
            PricingFamily.AnimalBuilding when lineItem.AnimalTier is not null =>
                I18nHelper.Get(
                    "ui.summary.price_line_animal",
                    new { service = taskLabel, tier = Humanize(lineItem.AnimalTier.Value.ToString()), count = lineItem.Quantity }),
            PricingFamily.Greenhouse =>
                I18nHelper.Get("ui.summary.price_line_greenhouse", new { service = taskLabel }),
            _ =>
                taskLabel,
        };
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

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var chars = new List<char>(value.Length + 4) { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
                chars.Add(' ');

            chars.Add(value[i]);
        }

        return new string(chars.ToArray());
    }

    private string GetPaymentTimingText(PaymentTimingKind kind) => kind switch
    {
        PaymentTimingKind.RecurringStartsNextEligibleDay => I18nHelper.Get("ui.summary.payment_timing_recurring"),
        PaymentTimingKind.RecurringEditAppliesNextEligibleDay => I18nHelper.Get("ui.summary.payment_timing_recurring_edit"),
        _ => I18nHelper.Get("ui.summary.payment_timing_one_time"),
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
}
