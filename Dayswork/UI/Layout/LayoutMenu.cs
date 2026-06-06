using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal abstract class LayoutMenu : IClickableMenu
{
    private ILayoutElement _root = null!;
    private LayoutContext _ctx = null!;

    protected Action? BackAction { get; }

    protected LayoutMenu(int width, int height, Action? onBack = null)
        : base(0, 0, width, height)
    {
        BackAction = onBack;
        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;
    }

    protected abstract ILayoutElement BuildLayout();

    protected void Rebuild()
    {
        _ctx = new LayoutContext();
        _root = BuildLayout();
        _root.Measure(width);
        _root.Arrange(new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), _ctx);
        _ctx.WireNavigation();
        populateClickableComponentList();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_ctx.ScrollTarget is { } scroll && scroll.HandleScrollClick(x, y))
        {
            Rebuild();
            return;
        }

        _ctx.HandleClick(x, y);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (_ctx.ScrollTarget is { } scroll && scroll.HandleScrollDrag(x, y))
            Rebuild();
    }

    public override void releaseLeftClick(int x, int y)
    {
        _ctx.ScrollTarget?.ReleaseScrollDrag();
        base.releaseLeftClick(x, y);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction != 0 && _ctx.ScrollTarget is { } scroll)
        {
            scroll.HandleScrollWheel(direction);
            Rebuild();
        }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B)
        {
            BackAction?.Invoke();
            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        if (_ctx is not null)
            allClickableComponents.AddRange(_ctx.GetAllComponents());
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _ctx?.GetDefaultSnapTarget() ?? getComponentWithID(0);
        snapCursorToCurrentSnappedComponent();
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        _root?.Draw(b);
        drawMouse(b);
    }
}
