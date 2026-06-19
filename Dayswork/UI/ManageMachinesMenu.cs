using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.UI;

internal sealed class ManageMachinesMenu : LayoutMenu
{
    private const int GroupColW = 96;
    private const int CountColW = 140;
    private const int ModeColW = 150;
    private const int ActionButtonW = 82;
    private const int RowHeight = 88;
    private const int RowGap = 8;
    private const int MaxVisibleRows = 5;

    private static readonly Color SecondaryTextColor = new(96, 72, 48);
    private static readonly Color HeaderColor = new(80, 60, 40);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onAddGroup;
    private readonly Action<ContractDraft, string> _onEditGroup;
    private readonly Action<ContractDraft, string> _onDeleteGroup;
    private int _scrollIndex;

    public ManageMachinesMenu(
        ContractDraft draft,
        Action<ContractDraft> onBack,
        Action<ContractDraft> onAddGroup,
        Action<ContractDraft, string> onEditGroup,
        Action<ContractDraft, string> onDeleteGroup)
        : base(ContractMenuLayout.ManageCropsWidth, ContractMenuLayout.ManageCropsHeight,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _onAddGroup = onAddGroup;
        _onEditGroup = onEditGroup;
        _onDeleteGroup = onDeleteGroup;
        Rebuild();
    }

    private MachinePlanDraft Plan => _draft.MachinePlan;

    protected override ILayoutElement BuildLayout()
    {
        var content = new List<ILayoutElement>
        {
            new HStack(0,
                HStack.Fill(new Spacer(0)),
                HStack.Auto(new MenuButton(
                    I18nHelper.Get("ui.manage_machines.add_group_btn"),
                    () => _onAddGroup(_draft),
                    fixedWidth: 250,
                    height: 52))),
            new Spacer(14),
        };

        if (Plan.Groups.Count == 0)
        {
            content.Add(new Label(I18nHelper.Get("ui.manage_machines.no_groups"), color: SecondaryTextColor));
        }
        else
        {
            content.Add(BuildHeaderRow());
            content.Add(new Spacer(6));
            content.Add(new FixedHeight(
                new ScrollPanel(
                    () => Plan.Groups.Select((group, index) => BuildGroupRow(group, index + 1)).ToList(),
                    RowHeight,
                    gap: RowGap,
                    scrollIndex: _scrollIndex,
                    onScroll: index => _scrollIndex = index),
                GetGroupListHeight()));
        }

        return new PageShell(
            title: I18nHelper.Get("ui.manage_machines.title"),
            description: I18nHelper.Get("ui.manage_machines.summary_help"),
            onBack: BackAction!,
            content: new VStack(0, content.ToArray()),
            footerBottomPadding: 34);
    }

    private static ILayoutElement BuildHeaderRow() =>
        new HStack(8,
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_machines.header_group"), color: HeaderColor), GroupColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_machines.header_machines"), color: HeaderColor), CountColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_machines.header_mode"), color: HeaderColor), ModeColW),
            HStack.Fill(new Spacer(0)));

    private ILayoutElement BuildGroupRow(MachineGroupDraft group, int displayIndex)
    {
        var topLine = new HStack(8,
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_machines.group_label", new { index = displayIndex })), GroupColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_machines.machine_count", new { count = group.Machines.Count }), ellipsize: true), CountColW),
            HStack.Fixed(new Label(ModeLabel(group.Mode), ellipsize: true), ModeColW),
            HStack.Fill(new Spacer(0)),
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.manage_machines.edit_group_btn"),
                () => _onEditGroup(_draft, group.Id),
                fixedWidth: ActionButtonW,
                height: 42)),
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.manage_machines.delete_group_btn"),
                () => { _onDeleteGroup(_draft, group.Id); Rebuild(); },
                fixedWidth: ActionButtonW,
                height: 42,
                sound: "trashcan")));

        var detailLine = new Padding(
            new Label(DetailSummary(group), color: SecondaryTextColor),
            top: 2,
            left: GroupColW + 8);

        return new VStack(0, topLine, detailLine);
    }

    private string DetailSummary(MachineGroupDraft group)
    {
        var type = group.HasType
            ? ItemRegistry.GetData(group.MachineType!)?.DisplayName ?? group.MachineType!
            : I18nHelper.Get("ui.manage_machines.type_none");
        var input = group.IsAnyInput
            ? I18nHelper.Get("ui.manage_machines.input_any")
            : I18nHelper.Get("ui.manage_machines.input_specific", new { count = group.AllowedInputIds.Count });
        var chest = group.InputChest is { } chestRef
            ? I18nHelper.Get("ui.manage_machines.chest_at", new { x = chestRef.Tile.X, y = chestRef.Tile.Y })
            : I18nHelper.Get("ui.manage_machines.chest_none");
        var output = OutputLabel(group.OutputDestination);
        return I18nHelper.Get("ui.manage_machines.detail_summary", new { type, input, chest, output });
    }

    internal static string ModeLabel(MachineGroupMode mode) => mode switch
    {
        MachineGroupMode.CollectAndReload => I18nHelper.Get("ui.manage_machines.mode_collect_reload"),
        MachineGroupMode.CollectOnly => I18nHelper.Get("ui.manage_machines.mode_collect_only"),
        _ => mode.ToString(),
    };

    internal static string OutputLabel(DestinationKey? destination) => destination switch
    {
        ShippingBinDestination => I18nHelper.Get("ui.zone_chest.shipping_bin_option"),
        ChestDestination chest => I18nHelper.Get("ui.manage_machines.output_chest_at", new { x = chest.Ref.Tile.X, y = chest.Ref.Tile.Y }),
        _ => I18nHelper.Get("ui.manage_machines.output_automatic"),
    };

    private int GetGroupListHeight()
    {
        const int Overhead = 380;
        var maxByHeight = Math.Max(1, (height - Overhead + RowGap) / (RowHeight + RowGap));
        var visibleRows = Math.Clamp(Plan.Groups.Count, 1, Math.Min(MaxVisibleRows, maxByHeight));
        return visibleRows * RowHeight + Math.Max(0, visibleRows - 1) * RowGap;
    }
}
