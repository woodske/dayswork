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

    // Read rather than passed: on a remote client the answer that says what this player owns can
    // land while the page is already open (R8), and null means "not told yet" — not "owns nothing".
    private readonly Func<FarmhandUpgradeState?> _readState;
    private FarmhandUpgradeState? _state;
    private readonly Action<FarmhandUpgradeKind> _onPurchase;

    public UpgradesMenu(
        Func<FarmhandUpgradeState?> readState,
        Action<FarmhandUpgradeKind> onPurchase,
        Action onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onBack)
    {
        _readState = readState;
        _state = readState();
        _onPurchase = onPurchase;
        Rebuild();
    }

    /// <summary>Redraws the page as soon as the host's answer arrives, so a player who opened
    /// Upgrades before it landed does not sit looking at a stale "waiting" page.</summary>
    public override void update(GameTime time)
    {
        base.update(time);

        var current = _readState();
        if (current == _state)
            return;

        _state = current;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.upgrades.title"),
            description: I18nHelper.Get("ui.upgrades.description"),
            onBack: BackAction!,
            content: _state is null
                ? new VStack(14, new Label(I18nHelper.Get("ui.net.waiting_for_host"), color: SecondaryTextColor))
                : new VStack(14, FarmhandUpgradeCatalog.All.Select(BuildUpgradeRow).ToArray()));

    private ILayoutElement BuildUpgradeRow(FarmhandUpgradeDefinition definition)
    {
        var purchased = _state!.IsPurchased(definition.Kind);
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
                    enabled: !purchased && !locked && Sponsor.Money(Game1.player.UniqueMultiplayerID) >= definition.Price,
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
