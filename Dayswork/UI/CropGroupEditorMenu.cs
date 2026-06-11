using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;
using Season = Dayswork.Core.Domain.Season;

namespace Dayswork.UI;

internal sealed class CropGroupEditorMenu : LayoutMenu
{
    private const int RowHeight = 58;
    private const int ButtonHeight = 42;
    private const int SeasonColW = 132;
    private const int CropColW = 270;
    private const int FertColW = 230;
    private const int Gap = 14;
    private const int LocationButtonW = 210;

    private static readonly Color SecondaryTextColor = new(96, 72, 48);
    private static readonly Color HeaderColor = new(80, 60, 40);
    private static readonly Color LockedColor = new(150, 110, 70);

    private readonly ContractDraft _draft;
    private readonly CropGroupDraft _group;
    private readonly IReadOnlyList<CropGroupLocationOption> _locationOptions;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<string, Season> _onPickCrop;
    private readonly Action<string, Season> _onPickFertilizer;
    private readonly Action<string> _onPickChest;
    private readonly Action<string> _onBeginDraw;
    private readonly Action<string, string> _onSetLocation;

    public CropGroupEditorMenu(
        ContractDraft draft,
        CropGroupDraft group,
        IReadOnlyList<CropGroupLocationOption> locationOptions,
        Action<ContractDraft> onBack,
        Action<string, Season> onPickCrop,
        Action<string, Season> onPickFertilizer,
        Action<string> onPickChest,
        Action<string> onBeginDraw,
        Action<string, string> onSetLocation)
        : base(ContractMenuLayout.ManageCropsWidth, ContractMenuLayout.Height,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _group = group;
        _locationOptions = locationOptions;
        _onBack = onBack;
        _onPickCrop = onPickCrop;
        _onPickFertilizer = onPickFertilizer;
        _onPickChest = onPickChest;
        _onBeginDraw = onBeginDraw;
        _onSetLocation = onSetLocation;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout()
    {
        var rows = new List<ILayoutElement>
        {
            BuildLocationRow(),
            new Spacer(14),
            BuildHeaderRow(),
            new Spacer(4),
        };

        if (_group.IsSeasonAgnostic)
        {
            rows.Add(new FixedHeight(BuildYearRoundRow(), RowHeight));
        }
        else
        {
            foreach (var season in CropPlanDraft.AllSeasons)
            {
                rows.Add(new FixedHeight(
                    _group.LockState(season) == SeasonLockState.MultiSeasonLocked
                        ? BuildLockedRow(season)
                        : BuildEditableRow(season),
                    RowHeight));
            }
        }

        rows.Add(new Spacer(12));
        rows.Add(new Label(
            I18nHelper.Get("ui.manage_crops.output_label", new { name = OutputChestLabel() }),
            color: Game1.textColor));
        rows.Add(new Spacer(8));
        rows.Add(new MenuButton(
            I18nHelper.Get("ui.manage_crops.set_btn"),
            () => _onPickChest(_group.Id),
            fixedWidth: 260,
            height: ButtonHeight));

        rows.Add(new Spacer(18));
        rows.Add(BuildDrawRow());

        return new PageShell(
            title: I18nHelper.Get("ui.manage_crops.editor_title"),
            description: I18nHelper.Get("ui.manage_crops.editor_help"),
            onBack: () => _onBack(_draft),
            content: new VStack(0, rows.ToArray()));
    }

    private static ILayoutElement BuildHeaderRow() =>
        new HStack(Gap,
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_season"), color: HeaderColor), SeasonColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_crop"), color: HeaderColor), CropColW),
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_fertilizer"), color: HeaderColor), FertColW));

    private ILayoutElement BuildLocationRow()
    {
        var parts = new List<HStack.Column>
        {
            HStack.Fixed(new Label(I18nHelper.Get("ui.manage_crops.header_location"), color: HeaderColor), SeasonColW),
        };

        foreach (var option in _locationOptions)
        {
            var isSelected = string.Equals(option.LocationName, _group.LocationName, StringComparison.Ordinal);
            parts.Add(HStack.Auto(new MenuButton(
                isSelected
                    ? I18nHelper.Get("ui.manage_crops.location_selected", new { name = option.DisplayName })
                    : option.DisplayName,
                () => _onSetLocation(_group.Id, option.LocationName),
                enabled: option.IsAvailable,
                fixedWidth: LocationButtonW,
                height: ButtonHeight,
                textAlign: HAlign.Left)));
        }

        return new HStack(8, parts.ToArray());
    }

    private ILayoutElement BuildEditableRow(Season season)
    {
        var configured = _group.IsConfigured(season);
        var cropLabel = configured
            ? _group.DisplayCropName(season)
            : I18nHelper.Get("ui.manage_crops.choose_crop");

        return new HStack(Gap,
            HStack.Fixed(
                new Label(SeasonLabel(season)),
                SeasonColW),
            HStack.Fixed(
                new MenuButton(cropLabel, () => _onPickCrop(_group.Id, season),
                    fixedWidth: CropColW, height: ButtonHeight, textAlign: HAlign.Left),
                CropColW),
            HStack.Fixed(
                new MenuButton(FertilizerLabel(season),
                    () => _onPickFertilizer(_group.Id, season),
                    enabled: configured,
                    fixedWidth: FertColW, height: ButtonHeight, textAlign: HAlign.Left),
                FertColW));
    }

    private ILayoutElement BuildYearRoundRow()
    {
        var configured = _group.YearRoundSlot.HasCrop;
        var cropLabel = configured
            ? _group.YearRoundSlot.CropDisplayName
            : I18nHelper.Get("ui.manage_crops.choose_crop");

        return new HStack(Gap,
            HStack.Fixed(
                new Label(I18nHelper.Get("ui.manage_crops.year_round")),
                SeasonColW),
            HStack.Fixed(
                new MenuButton(cropLabel, () => _onPickCrop(_group.Id, Season.Spring),
                    fixedWidth: CropColW, height: ButtonHeight, textAlign: HAlign.Left),
                CropColW),
            HStack.Fixed(
                new MenuButton(FertilizerLabel(Season.Spring),
                    () => _onPickFertilizer(_group.Id, Season.Spring),
                    enabled: configured,
                    fixedWidth: FertColW, height: ButtonHeight, textAlign: HAlign.Left),
                FertColW));
    }

    private ILayoutElement BuildLockedRow(Season season)
    {
        var origin = _group.LockOrigin(season);
        var reason = I18nHelper.Get("ui.manage_crops.locked", new
        {
            crop = _group.DisplayCropName(season),
            origin = origin is { } o
                ? SeasonLabel(o)
                : string.Empty,
        });

        return new HStack(Gap,
            HStack.Fixed(
                new Label(
                    SeasonLabel(season),
                    color: LockedColor),
                SeasonColW),
            HStack.Fill(new Label(reason, color: LockedColor)));
    }

    private ILayoutElement BuildDrawRow()
    {
        var parts = new List<HStack.Column>
        {
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.manage_crops.draw_btn"),
                () => _onBeginDraw(_group.Id),
                enabled: _group.HasAnyConfiguredSeason,
                fixedWidth: 250,
                height: 52)),
            HStack.Auto(new Spacer(18)),
            HStack.Fill(new Label(
                I18nHelper.Get("ui.manage_crops.group_zones",
                    new { zones = _group.Zones.Count, tiles = CountTiles(_group.Zones) }),
                color: SecondaryTextColor)),
        };

        return new HStack(0, parts.ToArray());
    }

    private string FertilizerLabel(Season season)
    {
        if (_group.IsSeasonAgnostic)
        {
            if (!_group.YearRoundSlot.HasCrop)
                return I18nHelper.Get("ui.manage_crops.fertilizer_disabled");

            return string.IsNullOrEmpty(_group.YearRoundSlot.FertilizerDisplayName)
                ? I18nHelper.Get("ui.manage_crops.fertilizer_none")
                : _group.YearRoundSlot.FertilizerDisplayName;
        }

        if (!_group.IsConfigured(season))
            return I18nHelper.Get("ui.manage_crops.fertilizer_disabled");

        var slot = _group.Slot(season);
        return string.IsNullOrEmpty(slot.FertilizerDisplayName)
            ? I18nHelper.Get("ui.manage_crops.fertilizer_none")
            : slot.FertilizerDisplayName;
    }

    private string OutputChestLabel()
    {
        var chest = _group.OutputChest;
        return chest is null
            ? I18nHelper.Get("ui.manage_crops.output_automatic")
            : I18nHelper.Get("ui.manage_crops.output_chest_at", new { x = chest.Tile.X, y = chest.Tile.Y });
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
}
