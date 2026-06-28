using Dayswork.Core.Upgrades;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.UI;

internal sealed class UpgradesMenu : LayoutMenu
{
    private const int RowHeight = 118;
    private const int ButtonWidth = 150;
    private static readonly Color SecondaryTextColor = new(96, 72, 48);
    private static readonly Color PurchasedColor = new(34, 139, 34);

    private readonly FarmhandUpgradeState _state;
    private readonly Action<FarmhandUpgradeKind> _onPurchase;

    public UpgradesMenu(
        FarmhandUpgradeState state,
        Action<FarmhandUpgradeKind> onPurchase,
        Action onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onBack)
    {
        _state = state;
        _onPurchase = onPurchase;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.upgrades.title"),
            description: I18nHelper.Get("ui.upgrades.description"),
            onBack: BackAction!,
            content: new VStack(14,
                FarmhandUpgradeCatalog.All.Select(BuildUpgradeRow).ToArray()));

    private ILayoutElement BuildUpgradeRow(FarmhandUpgradeDefinition definition)
    {
        var purchased = _state.IsPurchased(definition.Kind);
        var locked = definition.Prerequisite is { } prerequisite && !_state.IsPurchased(prerequisite);
        var name = I18nHelper.Get(NameKey(definition.Kind));
        var detail = I18nHelper.Get(DetailKey(definition.Kind), new { price = definition.Price });

        string statusText;
        Color statusColor;
        if (purchased)
        {
            statusText = I18nHelper.Get("ui.upgrades.purchased");
            statusColor = PurchasedColor;
        }
        else if (locked)
        {
            statusText = I18nHelper.Get("ui.upgrades.locked");
            statusColor = SecondaryTextColor;
        }
        else
        {
            statusText = string.Empty;
            statusColor = PurchasedColor;
        }

        return new FixedHeight(
            new HStack(12,
                HStack.Fill(new VStack(8,
                    new Label(name),
                    new Label(detail, color: SecondaryTextColor, wrap: true),
                    new Label(statusText, color: statusColor))),
                HStack.Auto(new MenuButton(
                    I18nHelper.Get("ui.upgrades.purchase_btn"),
                    () => _onPurchase(definition.Kind),
                    enabled: !purchased && !locked && Game1.player.Money >= definition.Price,
                    fixedWidth: ButtonWidth,
                    height: 52))),
            RowHeight);
    }

    private static string NameKey(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => "ui.upgrades.speed.name",
            FarmhandUpgradeKind.Speed2 => "ui.upgrades.speed2.name",
            _ => "ui.upgrades.energy.name",
        };

    private static string DetailKey(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => "ui.upgrades.speed.detail",
            FarmhandUpgradeKind.Speed2 => "ui.upgrades.speed2.detail",
            _ => "ui.upgrades.energy.detail",
        };
}
