using Dayswork.Core.Inventory;
using Dayswork.Orchestration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class ManagedShoppingInterruptionTests
{
    [Fact]
    public void Dayswork2Review_R2_MissingInputChestRoutesEveryPaidItemExactlyOnce()
    {
        var purchased = new FakeItem("paid-seeds", quantity: 7);
        var carried = new List<Item> { purchased };
        var overflow = new List<(Item Item, OverflowReason Reason)>();

        var deposited = ManagedShoppingCoordinator.SettleCarriedItems(
            carried,
            inputChest: null,
            (item, reason) => overflow.Add((item, reason)));
        var depositedAgain = ManagedShoppingCoordinator.SettleCarriedItems(
            carried,
            inputChest: null,
            (item, reason) => overflow.Add((item, reason)));

        Assert.Equal(0, deposited);
        Assert.Equal(0, depositedAgain);
        Assert.Empty(carried);
        var routed = Assert.Single(overflow);
        Assert.Same(purchased, routed.Item);
        Assert.Equal(7, routed.Item.Stack);
        Assert.Equal(OverflowReason.ChestMissing, routed.Reason);
    }

    [Fact]
    public void Dayswork2Review_R2_FullInputChestRoutesTheUnchangedPaidItemToOverflow()
    {
        var purchased = new FakeItem("paid-seeds", quantity: 7);
        var carried = new List<Item> { purchased };
        var overflow = new List<(Item Item, OverflowReason Reason)>();

        var deposited = ManagedShoppingCoordinator.SettleCarriedItems(
            carried,
            tryAddToInputChest: item => item,
            (item, reason) => overflow.Add((item, reason)));

        Assert.Equal(0, deposited);
        Assert.Empty(carried);
        var routed = Assert.Single(overflow);
        Assert.Same(purchased, routed.Item);
        Assert.Equal(7, routed.Item.Stack);
        Assert.Equal(OverflowReason.ChestFull, routed.Reason);
    }

    [Fact]
    public void Dayswork2Review_R2_PartialChestDepositConservesPurchasedQuantity()
    {
        var purchased = new FakeItem("paid-seeds", quantity: 7);
        var carried = new List<Item> { purchased };
        var overflow = new List<(Item Item, OverflowReason Reason)>();

        var deposited = ManagedShoppingCoordinator.SettleCarriedItems(
            carried,
            tryAddToInputChest: item =>
            {
                item.Stack -= 1;
                return item;
            },
            (item, reason) => overflow.Add((item, reason)));

        Assert.Equal(1, deposited);
        var routed = Assert.Single(overflow);
        Assert.Equal(6, routed.Item.Stack);
        Assert.Equal(7, deposited + routed.Item.Stack);
        Assert.Equal(OverflowReason.ChestFull, routed.Reason);
    }

    private sealed class FakeItem : Item
    {
        private readonly int _maximumStack;

        public FakeItem(string id, int quantity, int maximumStack = 999)
        {
            _maximumStack = maximumStack;
            ItemId = id;
            Name = id;
            Stack = quantity;
        }

        public override string TypeDefinitionId => "(TEST)";

        public override string DisplayName => Name;

        public override void drawInMenu(
            SpriteBatch spriteBatch,
            Vector2 location,
            float scaleSize,
            float transparency,
            float layerDepth,
            StackDrawType drawStackNumber,
            Color color,
            bool drawShadow)
        {
        }

        public override int maximumStackSize() => _maximumStack;

        public override string getDescription() => Name;

        public override bool isPlaceable() => false;

        protected override Item GetOneNew() => new FakeItem(ItemId, 1, _maximumStack);
    }
}
