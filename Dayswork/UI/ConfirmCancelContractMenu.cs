using Dayswork.Integration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;

namespace Dayswork.UI;

// Shown when the player clicks Cancel on an existing contract — confirms the destructive action.
internal sealed class ConfirmCancelContractMenu : LayoutMenu
{
    public ConfirmCancelContractMenu(Action onGoBack, Action onConfirm)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: onGoBack)
    {
        _onGoBack  = onGoBack;
        _onConfirm = onConfirm;
        Rebuild();
    }

    private readonly Action _onGoBack;
    private readonly Action _onConfirm;

    protected override ILayoutElement BuildLayout()
    {
        var buttons = new HStack(16,
            HStack.Fill(new Spacer(0)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.confirm_cancel_contract.go_back_btn"), _onGoBack)),
            HStack.Auto(new MenuButton(I18nHelper.Get("ui.confirm_cancel_contract.confirm_btn"), _onConfirm)),
            HStack.Fill(new Spacer(0)));

        return new VStack(0,
            new Padding(
                new Label(I18nHelper.Get("ui.confirm_cancel_contract.title"), LabelFont.Dialogue),
                top: 20, left: 40, right: 40),
            new Padding(
                new Label(I18nHelper.Get("ui.confirm_cancel_contract.description"), wrap: true),
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
