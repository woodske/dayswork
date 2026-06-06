using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley.Menus;
using Xunit;

namespace Dayswork.Tests.UI.Layout;

public class LayoutContextTests
{
    [Fact]
    public void WireNavigation_VerticalStack_WiresUpDown()
    {
        var ctx = new LayoutContext();
        var a = ctx.Register(new Rectangle(0, 0, 100, 50), "a", "A", null);
        var b = ctx.Register(new Rectangle(0, 60, 100, 50), "b", "B", null);
        var c = ctx.Register(new Rectangle(0, 120, 100, 50), "c", "C", null);

        ctx.WireNavigation();

        Assert.Equal(-1, a.upNeighborID);
        Assert.Equal(b.myID, a.downNeighborID);
        Assert.Equal(a.myID, b.upNeighborID);
        Assert.Equal(c.myID, b.downNeighborID);
        Assert.Equal(b.myID, c.upNeighborID);
        Assert.Equal(-1, c.downNeighborID);
    }

    [Fact]
    public void WireNavigation_HorizontalRow_WiresLeftRight()
    {
        var ctx = new LayoutContext();
        var a = ctx.Register(new Rectangle(0, 0, 80, 50), "a", "A", null);
        var b = ctx.Register(new Rectangle(100, 0, 80, 50), "b", "B", null);
        var c = ctx.Register(new Rectangle(200, 0, 80, 50), "c", "C", null);

        ctx.WireNavigation();

        Assert.Equal(-1, a.leftNeighborID);
        Assert.Equal(b.myID, a.rightNeighborID);
        Assert.Equal(a.myID, b.leftNeighborID);
        Assert.Equal(c.myID, b.rightNeighborID);
        Assert.Equal(b.myID, c.leftNeighborID);
        Assert.Equal(-1, c.rightNeighborID);
    }

    [Fact]
    public void WireNavigation_Grid_WiresAllDirections()
    {
        var ctx = new LayoutContext();
        var topLeft = ctx.Register(new Rectangle(0, 0, 80, 40), "tl", "TL", null);
        var topRight = ctx.Register(new Rectangle(100, 0, 80, 40), "tr", "TR", null);
        var botLeft = ctx.Register(new Rectangle(0, 60, 80, 40), "bl", "BL", null);
        var botRight = ctx.Register(new Rectangle(100, 60, 80, 40), "br", "BR", null);

        ctx.WireNavigation();

        Assert.Equal(topRight.myID, topLeft.rightNeighborID);
        Assert.Equal(botLeft.myID, topLeft.downNeighborID);

        Assert.Equal(topLeft.myID, topRight.leftNeighborID);
        Assert.Equal(botRight.myID, topRight.downNeighborID);

        Assert.Equal(topLeft.myID, botLeft.upNeighborID);
        Assert.Equal(botRight.myID, botLeft.rightNeighborID);

        Assert.Equal(topRight.myID, botRight.upNeighborID);
        Assert.Equal(botLeft.myID, botRight.leftNeighborID);
    }

    [Fact]
    public void HandleClick_InBounds_FiresCallback()
    {
        var ctx = new LayoutContext();
        var fired = false;
        ctx.Register(new Rectangle(10, 10, 100, 50), "btn", "Test", () => fired = true);

        var handled = ctx.HandleClick(50, 30);

        Assert.True(handled);
        Assert.True(fired);
    }

    [Fact]
    public void HandleClick_OutOfBounds_ReturnsFalse()
    {
        var ctx = new LayoutContext();
        var fired = false;
        ctx.Register(new Rectangle(10, 10, 100, 50), "btn", "Test", () => fired = true);

        var handled = ctx.HandleClick(200, 200);

        Assert.False(handled);
        Assert.False(fired);
    }

    [Fact]
    public void HandleClick_NullCallback_ReturnsFalse()
    {
        var ctx = new LayoutContext();
        ctx.Register(new Rectangle(10, 10, 100, 50), "label", "Text", null);

        var handled = ctx.HandleClick(50, 30);

        Assert.False(handled);
    }

    [Fact]
    public void GetDefaultSnapTarget_ReturnsFirstRegistered()
    {
        var ctx = new LayoutContext();
        var first = ctx.Register(new Rectangle(0, 0, 50, 50), "a", "A", null);
        ctx.Register(new Rectangle(0, 60, 50, 50), "b", "B", null);

        Assert.Equal(first, ctx.GetDefaultSnapTarget());
    }

    [Fact]
    public void GetDefaultSnapTarget_Empty_ReturnsNull()
    {
        var ctx = new LayoutContext();
        Assert.Null(ctx.GetDefaultSnapTarget());
    }

    [Fact]
    public void GetAllComponents_ReturnsAllRegistered()
    {
        var ctx = new LayoutContext();
        ctx.Register(new Rectangle(0, 0, 50, 50), "a", "A", null);
        ctx.Register(new Rectangle(0, 60, 50, 50), "b", "B", null);

        var all = ctx.GetAllComponents();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void WireNavigation_UnevenGrid_NearestByX()
    {
        var ctx = new LayoutContext();
        var top = ctx.Register(new Rectangle(50, 0, 80, 40), "top", "T", null);
        var botLeft = ctx.Register(new Rectangle(0, 60, 80, 40), "bl", "BL", null);
        var botRight = ctx.Register(new Rectangle(100, 60, 80, 40), "br", "BR", null);

        ctx.WireNavigation();

        Assert.Equal(botLeft.myID, top.downNeighborID);
        Assert.Equal(top.myID, botLeft.upNeighborID);
        Assert.Equal(top.myID, botRight.upNeighborID);
    }
}
