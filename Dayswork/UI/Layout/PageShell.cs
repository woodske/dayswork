using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class PageShell : ILayoutElement
{
    private static readonly Color DescriptionColor = new(96, 72, 48);

    private readonly ILayoutElement _inner;
    private Rectangle _bounds;

    public PageShell(
        string title,
        ILayoutElement content,
        Action onBack,
        string? description = null,
        IReadOnlyList<MenuButton>? footerButtons = null,
        int footerBottomPadding = 14)
    {
        var children = new List<ILayoutElement>();

        children.Add(new Padding(
            new Label(title, LabelFont.Dialogue),
            top: 20, left: 40, right: 40));

        if (description is not null)
        {
            children.Add(new Padding(
                new Label(description, color: DescriptionColor, wrap: true),
                top: 8, left: 48, right: 48));
        }

        children.Add(new Padding(content, top: 12, left: 48, right: 48, bottom: 0));
        children.Add(Spacer.Fill);

        var footerParts = new List<HStack.Column>
        {
            HStack.Auto(new MenuButton(
                I18nHelper.Get("ui.common.back_btn"), onBack, sound: "bigDeSelect")),
        };

        if (footerButtons is { Count: > 0 })
        {
            footerParts.Add(HStack.Fill(new Spacer(0)));
            foreach (var btn in footerButtons)
                footerParts.Add(HStack.Auto(btn));
        }

        children.Add(new Padding(
            new HStack(8, footerParts.ToArray()),
            left: 40, right: 40, bottom: footerBottomPadding));

        _inner = new VStack(0, children.ToArray());
    }

    public int Measure(int availableWidth) => _inner.Measure(availableWidth);

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = bounds;
        _inner.Arrange(bounds, ctx);
    }

    public void Draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(
            b, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, Color.White);
        _inner.Draw(b);
    }
}
