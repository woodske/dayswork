using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;

namespace Dayswork.UI;

/// <summary>
/// Manage Fish Ponds page: pick the ponds to service on the map and the single output destination for
/// everything they produce. Collect-only — far simpler than <see cref="ManageMachinesMenu"/> (no
/// groups, no input filter/chest/mode), so this is a flat two-action page.
/// </summary>
internal sealed class ManageFishPondsMenu : LayoutMenu
{
    private static readonly Color SecondaryTextColor = new(96, 72, 48);

    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onSelectPonds;
    private readonly Action<ContractDraft> _onPickOutput;

    public ManageFishPondsMenu(
        ContractDraft draft,
        Action<ContractDraft> onBack,
        Action<ContractDraft> onSelectPonds,
        Action<ContractDraft> onPickOutput)
        : base(ContractMenuLayout.ManageCropsWidth, ContractMenuLayout.ManageCropsHeight,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _onSelectPonds = onSelectPonds;
        _onPickOutput = onPickOutput;
        Rebuild();
    }

    private FishPondPlanDraft Plan => _draft.FishPondPlan;

    protected override ILayoutElement BuildLayout()
    {
        var content = new List<ILayoutElement>
        {
            new HStack(8,
                HStack.Fixed(new Label(I18nHelper.Get("ui.manage_fish_ponds.ponds_label")), 220),
                HStack.Fill(new Label(
                    I18nHelper.Get("ui.manage_fish_ponds.selected_count", new { count = Plan.Ponds.Count }),
                    color: SecondaryTextColor, ellipsize: true)),
                HStack.Auto(new MenuButton(
                    I18nHelper.Get("ui.manage_fish_ponds.select_ponds_btn"),
                    () => _onSelectPonds(_draft),
                    fixedWidth: 220,
                    height: 52))),
            new Spacer(16),
            new HStack(8,
                HStack.Fixed(new Label(I18nHelper.Get("ui.manage_fish_ponds.output_label")), 220),
                HStack.Fill(new Label(
                    ManageMachinesMenu.OutputLabel(Plan.OutputDestination),
                    color: SecondaryTextColor, ellipsize: true)),
                HStack.Auto(new MenuButton(
                    I18nHelper.Get("ui.manage_fish_ponds.output_btn"),
                    () => _onPickOutput(_draft),
                    fixedWidth: 220,
                    height: 52))),
        };

        return new PageShell(
            title: I18nHelper.Get("ui.manage_fish_ponds.title"),
            description: I18nHelper.Get("ui.manage_fish_ponds.summary_help"),
            onBack: BackAction!,
            content: new VStack(0, content.ToArray()),
            footerBottomPadding: 34);
    }
}
