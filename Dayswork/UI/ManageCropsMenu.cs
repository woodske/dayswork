using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;
using Season = Dayswork.Core.Domain.Season;

namespace Dayswork.UI;

internal sealed class ManageCropsMenu : LayoutMenu
{
    private const int GroupColW = 96;
    private const int LocationColW = 132;
    private const int SeasonColW = 132;
    private const int ActionButtonW = 82;
    private const int RowHeight = 88;
    private const int RowGap = 8;
    private const int MaxVisibleRows = 4;

    private static readonly Color SecondaryTextColor = new(96, 72, 48);
    private static readonly Color HeaderColor = new(80, 60, 40);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onAddGroup;
    private readonly Action<ContractDraft, string> _onEditGroup;
    private readonly Action<ContractDraft, string> _onDeleteGroup;
    private int _scrollIndex;

    public ManageCropsMenu(
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

    private CropPlanDraft Plan => _draft.CropPlan;

    protected override ILayoutElement BuildLayout()
    {
        var content = new List<ILayoutElement>
        {
            new ToggleRow(
                I18nHelper.Get("ui.manage_crops.buy_from_joja_first"),
                Plan.BuyFromJojaFirst,
                () => { Plan.BuyFromJojaFirst = !Plan.BuyFromJojaFirst; Rebuild(); }),
            new Spacer(10),
            new MenuButton(
                I18nHelper.Get("ui.manage_crops.add_group_btn"),
                () => _onAddGroup(_draft),
                fixedWidth: 250,
                height: 52),
            new Spacer(14),
        };

        if (Plan.Groups.Count == 0)
        {
            content.Add(new Label(I18nHelper.Get("ui.manage_crops.no_groups"), color: SecondaryTextColor));
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
            title: I18nHelper.Get("ui.manage_crops.title"),
            description: I18nHelper.Get("ui.manage_crops.summary_help"),
            onBack: BackAction!,
            content: new VStack(0, content.ToArray()),
            footerBottomPadding: 34);
    }

    private static ILayoutElement BuildHeaderRow() =>
        new HStack(8,
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_group"), color: HeaderColor), GroupColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_location"), color: HeaderColor), LocationColW),
            HStack.Fixed(new Label(SeasonLabel(Season.Spring), color: HeaderColor), SeasonColW),
            HStack.Fixed(new Label(SeasonLabel(Season.Summer), color: HeaderColor), SeasonColW),
            HStack.Fixed(new Label(SeasonLabel(Season.Fall), color: HeaderColor), SeasonColW),
            HStack.Fixed(new Label(SeasonLabel(Season.Winter), color: HeaderColor), SeasonColW),
            HStack.Fill(new Spacer(0)));

    private ILayoutElement BuildGroupRow(CropGroupDraft group, int displayIndex)
    {
        var topLine = new HStack(8,
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.group_label", new { index = displayIndex })), GroupColW),
            HStack.Fixed(new Label(LocationLabel(group.LocationName), ellipsize: true), LocationColW),
            HStack.Fixed(new Label(SeasonSummary(group, Season.Spring), ellipsize: true), SeasonColW),
            HStack.Fixed(new Label(SeasonSummary(group, Season.Summer), ellipsize: true), SeasonColW),
            HStack.Fixed(new Label(SeasonSummary(group, Season.Fall), ellipsize: true), SeasonColW),
            HStack.Fixed(new Label(SeasonSummary(group, Season.Winter), ellipsize: true), SeasonColW),
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.manage_crops.edit_group_btn"),
                () => _onEditGroup(_draft, group.Id),
                fixedWidth: ActionButtonW,
                height: 42)),
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.manage_crops.delete_group_btn"),
                () => { _onDeleteGroup(_draft, group.Id); Rebuild(); },
                fixedWidth: ActionButtonW,
                height: 42,
                sound: "trashcan")));

        var detailLine = new Padding(
            new Label(I18nHelper.Get("ui.manage_crops.group_zones",
                new { zones = group.Zones.Count, tiles = CountTiles(group.Zones) }), color: SecondaryTextColor),
            top: 2,
            left: GroupColW + 8);

        return new VStack(0, topLine, detailLine);
    }

    private static string SeasonSummary(CropGroupDraft group, Season season)
    {
        if (group.IsSeasonAgnostic)
            return season == Season.Spring && group.YearRoundSlot.HasCrop
                ? group.YearRoundSlot.CropDisplayName
                : I18nHelper.Get("ui.manage_crops.summary_unassigned");

        if (!group.IsConfigured(season) && group.LockOrigin(season) is null)
            return I18nHelper.Get("ui.manage_crops.summary_unassigned");

        return group.DisplayCropName(season);
    }

    private int GetGroupListHeight()
    {
        var visibleRows = Math.Clamp(Plan.Groups.Count, 1, MaxVisibleRows);
        return visibleRows * RowHeight + Math.Max(0, visibleRows - 1) * RowGap;
    }

    private static int CountTiles(IEnumerable<Zone> zones) =>
        zones.Sum(zone =>
            (zone.BottomRight.X - zone.TopLeft.X + 1)
            * (zone.BottomRight.Y - zone.TopLeft.Y + 1));

    private static string SeasonLabel(Season season) => season switch
    {
        Season.Spring => I18nHelper.Get("ui.manage_crops.season.spring"),
        Season.Summer => I18nHelper.Get("ui.manage_crops.season.summer"),
        Season.Fall => I18nHelper.Get("ui.manage_crops.season.fall"),
        Season.Winter => I18nHelper.Get("ui.manage_crops.season.winter"),
        _ => season.ToString(),
    };

    private static string LocationLabel(string locationName)
    {
        if (string.Equals(locationName, "Farm", StringComparison.Ordinal))
            return I18nHelper.Get("ui.manage_crops.location_farm");

        if (string.Equals(locationName, "Greenhouse", StringComparison.Ordinal))
            return I18nHelper.Get("ui.manage_crops.location_greenhouse");

        return locationName;
    }
}
