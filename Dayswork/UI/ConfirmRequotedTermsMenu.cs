using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;

namespace Dayswork.UI;

// Shown when the host's terms are not the ones the player reviewed — its pricing or energy
// configuration differs from what this client quoted against (R9). Nothing has been charged yet:
// the player sees both figures and decides, rather than being billed a price they never agreed to.
internal sealed class ConfirmRequotedTermsMenu : LayoutMenu
{
    public ConfirmRequotedTermsMenu(
        int quotedPrice,
        ContractTermsSnapshot hostTerms,
        Action onAccept,
        Action onDecline)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onDecline)
    {
        _quotedPrice = quotedPrice;
        _hostTerms = hostTerms;
        _onAccept = onAccept;
        _onDecline = onDecline;
        Rebuild();
    }

    private readonly int _quotedPrice;
    private readonly ContractTermsSnapshot _hostTerms;
    private readonly Action _onAccept;
    private readonly Action _onDecline;

    protected override ILayoutElement BuildLayout()
    {
        var buttons = new HStack(16,
            HStack.Fill(new Spacer(0)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.requoted_terms.accept_btn"), _onAccept)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.requoted_terms.decline_btn"), _onDecline)),
            HStack.Fill(new Spacer(0)));

        return new VStack(0,
            new Padding(
                new Label(I18nHelper.Get("ui.requoted_terms.title"), LabelFont.Dialogue),
                top: 20, left: 40, right: 40),
            new Padding(
                new Label(I18nHelper.Get("ui.requoted_terms.description"), wrap: true),
                top: 8, left: 48, right: 48),
            new Padding(
                new Label(I18nHelper.Get("ui.requoted_terms.was", new { amount = _quotedPrice })),
                top: 12, left: 48, right: 48),
            new Padding(
                new Label(I18nHelper.Get("ui.requoted_terms.now", new
                {
                    amount = _hostTerms.Pricing.TotalPrice,
                    energy = _hostTerms.Energy.DailyCapacity,
                })),
                top: 4, left: 48, right: 48),
            Spacer.Fill,
            new Padding(buttons, left: 40, right: 40, bottom: 0),
            Spacer.Fill);
    }

    public override void draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        base.draw(b);
    }
}
