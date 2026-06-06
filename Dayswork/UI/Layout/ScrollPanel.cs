using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.UI.Layout;

internal sealed class ScrollPanel : ILayoutElement, IScrollTarget
{
    private readonly Func<IReadOnlyList<ILayoutElement>> _itemFactory;
    private readonly int _itemHeight;
    private readonly int _gap;
    private readonly Action<int> _onScroll;

    private IReadOnlyList<ILayoutElement> _items = Array.Empty<ILayoutElement>();
    private Rectangle _viewportRect;
    private int _scrollIndex;
    private int _maxScrollIndex;
    private int _visibleCount;
    private bool _dragging;
    private int _dragOffset;

    public ScrollPanel(
        Func<IReadOnlyList<ILayoutElement>> itemFactory,
        int itemHeight,
        int gap = 0,
        int scrollIndex = 0,
        Action<int>? onScroll = null)
    {
        _itemFactory = itemFactory;
        _itemHeight = itemHeight;
        _gap = gap;
        _scrollIndex = scrollIndex;
        _onScroll = onScroll ?? (_ => { });
    }

    public int Measure(int availableWidth) => 0;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _items = _itemFactory();
        _visibleCount = GetVisibleCount(bounds.Height);
        _maxScrollIndex = Math.Max(0, _items.Count - _visibleCount);

        var previousScrollIndex = _scrollIndex;
        _scrollIndex = Math.Clamp(_scrollIndex, 0, _maxScrollIndex);
        if (_scrollIndex != previousScrollIndex)
            _onScroll(_scrollIndex);

        var isScrollable = MenuScrollBar.IsVisible(_items.Count, _visibleCount);

        _viewportRect = new Rectangle(
            bounds.X,
            bounds.Y,
            bounds.Width - (isScrollable ? MenuScrollBar.ReservedWidth : 0),
            bounds.Height);

        var y = _viewportRect.Y;
        for (var i = _scrollIndex; i < _items.Count && i < _scrollIndex + _visibleCount; i++)
        {
            var itemBounds = new Rectangle(_viewportRect.X, y, _viewportRect.Width, _itemHeight);
            _items[i].Measure(_viewportRect.Width);
            _items[i].Arrange(itemBounds, ctx);
            y += _itemHeight + _gap;
        }

        if (isScrollable)
            ctx.ScrollTarget = this;
    }

    public void Draw(SpriteBatch b)
    {
        for (var i = _scrollIndex; i < _items.Count && i < _scrollIndex + _visibleCount; i++)
            _items[i].Draw(b);

        MenuScrollBar.Draw(b, _viewportRect, _visibleCount, _items.Count, _scrollIndex);
    }

    public void HandleScrollWheel(int direction)
    {
        Scroll(direction > 0 ? -1 : 1);
    }

    public bool HandleScrollClick(int x, int y)
    {
        if (MenuScrollBar.TryBeginDrag(
            _viewportRect, _visibleCount, _items.Count, _scrollIndex, x, y, out _dragOffset))
        {
            _dragging = true;
            return true;
        }

        if (MenuScrollBar.UpArrowContains(_viewportRect, _items.Count, _visibleCount, x, y))
        {
            Scroll(-1);
            return true;
        }

        if (MenuScrollBar.DownArrowContains(_viewportRect, _items.Count, _visibleCount, x, y))
        {
            Scroll(1);
            return true;
        }

        if (MenuScrollBar.TrackContains(_viewportRect, _visibleCount, _items.Count, x, y))
        {
            var next = MenuScrollBar.GetTrackClickScrollIndex(
                _viewportRect, _visibleCount, _items.Count, _scrollIndex, y);
            if (next != _scrollIndex)
            {
                _scrollIndex = next;
                _onScroll(_scrollIndex);
            }

            return true;
        }

        return false;
    }

    public bool HandleScrollDrag(int x, int y)
    {
        if (!_dragging)
            return false;

        var next = MenuScrollBar.GetDragScrollIndex(
            _viewportRect, _visibleCount, _items.Count, _dragOffset, y);

        if (next != _scrollIndex)
        {
            _scrollIndex = next;
            _onScroll(_scrollIndex);
        }

        return true;
    }

    public void ReleaseScrollDrag()
    {
        _dragging = false;
    }

    private void Scroll(int delta)
    {
        var next = Math.Clamp(_scrollIndex + delta, 0, _maxScrollIndex);
        if (next == _scrollIndex)
            return;

        _scrollIndex = next;
        _onScroll(_scrollIndex);
    }

    private int GetVisibleCount(int height) =>
        Math.Max(1, (height + _gap) / (_itemHeight + _gap));
}
