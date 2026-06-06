using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class SelectableCard : ILayoutElement
{
    private readonly ILayoutElement _content;
    private readonly bool _isSelected;
    private readonly Action _onClick;
    private readonly int _height;

    private Rectangle _bounds;

    public SelectableCard(ILayoutElement content, bool isSelected, Action onClick, int height)
    {
        _content = content;
        _isSelected = isSelected;
        _onClick = onClick;
        _height = height;
    }

    public int Measure(int availableWidth) => _height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, _height);

        ctx.Register(_bounds, "card", string.Empty, () =>
        {
            Game1.playSound("smallSelect");
            _onClick();
        });

        var inner = new Rectangle(bounds.X + 20, bounds.Y + 20, bounds.Width - 40, _height - 40);
        _content.Arrange(inner, ctx);
    }

    public void Draw(SpriteBatch b)
    {
        var cardColor = _isSelected ? Color.LightSkyBlue : Color.White;
        IClickableMenu.drawTextureBox(
            b, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, cardColor);
        _content.Draw(b);
    }
}
