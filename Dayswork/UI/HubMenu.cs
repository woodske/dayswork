using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.UI;

// Landing page for the contract flow. Instead of a linear wizard, the player picks any section
// from here; each section page returns to this hub. The final Hire/Confirm action lives here and is
// gated on the same validity check the coordinator enforces. draw() reads only pre-computed state.
internal sealed class HubMenu : LayoutMenu
{
    private const int RowHeight = 60;
    private const int RowGap = 12;
    private const int FooterReservedHeight = 96;
    private const int HeaderReservedHeight = 132;

    private static readonly Color DoneColor = new(34, 139, 34);
    private static readonly Color NeedsSetupColor = new(178, 34, 34);
    private static readonly Color NeutralColor = new(96, 72, 48);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onConfirm;
    private readonly Action _onCancel;
    private readonly List<NavItem> _items = new();
    private int _scrollIndex;

    public HubMenu(
        ContractDraft draft,
        Action<ContractDraft> onTaskSelection,
        Action<ContractDraft> onWorkScope,
        Action<ContractDraft> onManageCrops,
        Action<ContractDraft> onManageMachines,
        Action<ContractDraft> onOutput,
        Action<ContractDraft> onPriority,
        Action<ContractDraft> onEnergy,
        Action<ContractDraft> onRecurrence,
        Action<ContractDraft> onSummary,
        Action<ContractDraft> onConfirm,
        Action onCancel)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onCancel)
    {
        _draft = draft;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        _items.Add(new NavItem("ui.hub.task_selection", onTaskSelection, TaskSelectionStatus));
        _items.Add(new NavItem("ui.hub.work_scope", onWorkScope, WorkScopeStatus));
        _items.Add(new NavItem("ui.hub.manage_crops", onManageCrops, ManageCropsStatus));
        _items.Add(new NavItem("ui.hub.manage_machines", onManageMachines, ManageMachinesStatus));
        _items.Add(new NavItem("ui.hub.output_destination", onOutput, OutputStatus));
        _items.Add(new NavItem("ui.hub.task_priority", onPriority, () => HubStatus.None));
        _items.Add(new NavItem("ui.hub.energy", onEnergy, EnergyStatus));
        _items.Add(new NavItem("ui.hub.recurrence", onRecurrence, RecurrenceStatus));
        _items.Add(new NavItem("ui.hub.summary", onSummary, () => HubStatus.None));

        Rebuild();
    }

    private bool CanConfirm => _draft.PreviewState.ReviewModel.CanConfirm;

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.hub.title"),
            onBack: _onCancel,
            backLabel: I18nHelper.Get("ui.hub.cancel_btn"),
            content: new FixedHeight(
                new ScrollPanel(
                    BuildNavButtons,
                    RowHeight,
                    gap: RowGap,
                    scrollIndex: _scrollIndex,
                    onScroll: index => _scrollIndex = index),
                GetBodyHeight()),
            footerButtons: new[]
            {
                new MenuButton(
                    I18nHelper.Get("ui.hub.confirm_btn"),
                    () => _onConfirm(_draft),
                    enabled: CanConfirm,
                    fixedWidth: 170),
            });

    private IReadOnlyList<ILayoutElement> BuildNavButtons() =>
        _items.Select(item =>
        {
            var status = item.Status();
            return (ILayoutElement)new MenuButton(
                I18nHelper.Get(item.LabelKey),
                () => item.Open(_draft),
                fixedWidth: null,
                height: RowHeight,
                trailingText: status.Text,
                trailingColor: status.Color,
                textAlign: HAlign.Left);
        }).ToList();

    private int GetBodyHeight() =>
        Math.Max(RowHeight, height - HeaderReservedHeight - FooterReservedHeight);

    private HubStatus TaskSelectionStatus() =>
        _draft.EnabledTasks.Count > 0 ? Done() : NeedsSetup();

    private HubStatus WorkScopeStatus() =>
        _draft.OutdoorZones.Count > 0 || _draft.AnimalBuildings.Count > 0 || _draft.Greenhouses.Count > 0
            ? Done()
            : NeedsSetup();

    private HubStatus OutputStatus() =>
        _draft.Destinations.Count > 0 ? Done() : Optional();

    // Crop management is opt-in: "Ready" once at least one zone has been drawn/configured.
    private HubStatus ManageCropsStatus() =>
        _draft.CropPlan.HasAnyAssignment ? Done() : Optional();

    // Machine management is opt-in: "Ready" once at least one machine has been selected.
    private HubStatus ManageMachinesStatus() =>
        _draft.MachinePlan.HasAnyAssignment ? Done() : Optional();

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
    }
}
