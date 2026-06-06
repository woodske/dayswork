using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal interface ILayoutElement
{
    int Measure(int availableWidth);
    void Arrange(Rectangle bounds, LayoutContext ctx);
    void Draw(SpriteBatch b);
}

internal interface IHasDesiredWidth
{
    int DesiredWidth { get; }
}

internal interface IFillable
{
    bool IsFill { get; }
}

internal interface IScrollTarget
{
    void HandleScrollWheel(int direction);
    bool HandleScrollClick(int x, int y);
    bool HandleScrollDrag(int x, int y);
    void ReleaseScrollDrag();
}
