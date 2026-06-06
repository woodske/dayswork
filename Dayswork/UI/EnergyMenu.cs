using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using StardewValley;

namespace Dayswork.UI;

internal sealed record EnergyTierOption(EnergyTier Tier, int? Energy, int? Price);

internal sealed class EnergyMenu : LayoutMenu
{
    private readonly ContractDraft _draft;
    private readonly IReadOnlyList<EnergyTierOption> _options;
    private readonly Action<ContractDraft, EnergyTier> _onSelectTier;

    public EnergyMenu(
        ContractDraft draft,
        IReadOnlyList<EnergyTierOption> options,
        Action<ContractDraft, EnergyTier> onSelectTier,
        Action<ContractDraft> onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _options = options;
        _onSelectTier = onSelectTier;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.energy.title"),
            description: I18nHelper.Get("ui.energy.description"),
            onBack: BackAction!,
            content: new VStack(16,
                _options.Select(BuildCard).ToArray()));

    private ILayoutElement BuildCard(EnergyTierOption option)
    {
        var tierName = I18nHelper.Get($"ui.summary.tier.{TierKey(option.Tier)}");
        var children = new List<ILayoutElement>
        {
            new Label(tierName,
                color: option.Tier == _draft.Tier
                    ? Microsoft.Xna.Framework.Color.DarkBlue
                    : Game1.textColor),
        };

        if (option.Energy is { } energy && option.Price is { } price)
            children.Add(new Label(I18nHelper.Get("ui.energy.option_detail", new { energy, price })));

        return new SelectableCard(
            content: new VStack(8, children.ToArray()),
            isSelected: option.Tier == _draft.Tier,
            onClick: () => _onSelectTier(_draft, option.Tier),
            height: 110);
    }

    private static string TierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
        _ => tier.ToString(),
    };
}
