using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;

namespace Dayswork.UI;

// Shown when the host refuses an edit because the contract moved on since the draft was built
// (R7 — a Pause from another screen, a cancel, someone else's edit). The player's work is not
// thrown away and it is not silently rebased either: they choose which of the two versions wins.
internal sealed class ConfirmStaleContractMenu : LayoutMenu
{
    public ConfirmStaleContractMenu(Action onReviewTheirs, Action onKeepMine)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onKeepMine)
    {
        _onReviewTheirs = onReviewTheirs;
        _onKeepMine = onKeepMine;
        Rebuild();
    }

    private readonly Action _onReviewTheirs;
    private readonly Action _onKeepMine;

    protected override ILayoutElement BuildLayout()
    {
        var buttons = new HStack(16,
            HStack.Fill(new Spacer(0)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.stale_contract.review_btn"), _onReviewTheirs)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.stale_contract.keep_btn"), _onKeepMine)),
            HStack.Fill(new Spacer(0)));

        return new VStack(0,
            new Padding(
                new Label(I18nHelper.Get("ui.stale_contract.title"), LabelFont.Dialogue),
                top: 20, left: 40, right: 40),
            new Padding(
                new Label(I18nHelper.Get("ui.stale_contract.description"), wrap: true),
                top: 8, left: 48, right: 48),
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
