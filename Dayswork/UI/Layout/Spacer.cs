using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal sealed class Spacer : ILayoutElement, IFillable, IHasDesiredWidth
{
    public static readonly Spacer Fill = new(0, true);

    private readonly int _fixedSize;

    public Spacer(int fixedSize, bool isFill = false)
    {
        _fixedSize = fixedSize;
        IsFill = isFill;
    }

    public bool IsFill { get; }

    public int DesiredWidth => _fixedSize;

    public int Measure(int availableWidth) => _fixedSize;

    public void Arrange(Rectangle bounds, LayoutContext ctx) { }

    public void Draw(SpriteBatch b) { }
}
