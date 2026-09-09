using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class TreeDropAttributionTests
{
    [Fact]
    public void Dayswork2Review_R6_ReferenceOwnershipKeepsEqualLookingTreesIndependent()
    {
        var firstTree = new EqualLookingTree();
        var secondTree = new EqualLookingTree();
        var ownership = new ReferenceOwnershipIndex<EqualLookingTree, string>();

        Assert.True(ownership.TryAdd(firstTree, "owner-a"));
        Assert.True(ownership.TryAdd(secondTree, "owner-b"));
        Assert.True(ownership.TryGetValue(firstTree, out var firstOwner));
        Assert.True(ownership.TryGetValue(secondTree, out var secondOwner));
        Assert.Equal("owner-a", firstOwner);
        Assert.Equal("owner-b", secondOwner);
        Assert.Equal(2, ownership.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dayswork2Review_R6_UpdateOrderCannotTransferAnotherTreesOwner(bool reverseOrder)
    {
        var treeA = new object();
        var treeB = new object();
        var ownership = new ReferenceOwnershipIndex<object, string>();
        ownership.TryAdd(treeA, "owner-a/chest-a");
        ownership.TryAdd(treeB, "owner-b/chest-b");

        var updates = reverseOrder ? new[] { treeB, treeA } : new[] { treeA, treeB };
        var routed = new List<string>();
        foreach (var tree in updates)
        {
            Assert.True(ownership.TryGetValue(tree, out var destination));
            routed.Add(destination);
            ownership.Remove(tree);
        }

        Assert.Equal(
            reverseOrder
                ? new[] { "owner-b/chest-b", "owner-a/chest-a" }
                : new[] { "owner-a/chest-a", "owner-b/chest-b" },
            routed);
        Assert.Equal(0, ownership.Count);
    }

    [Fact]
    public void Dayswork2Review_R6_ExactCallDeltaExcludesNearbyPreexistingPlayerDebris()
    {
        var playerDrop = new object();
        var treeDrop = new object();
        var baseline = new HashSet<object>(ReferenceEqualityComparer.Instance) { playerDrop };

        var emitted = TreeDropAttribution.SelectNewReferences(
            new[] { playerDrop, treeDrop },
            baseline);

        Assert.Single(emitted);
        Assert.Same(treeDrop, emitted[0]);
    }

    [Fact]
    public void Dayswork2Review_R6_FinalizerAndPostfixShareAnIdempotentCompletionGate()
    {
        var gate = new TreeDropAttribution.CompletionGate();

        Assert.True(gate.TryComplete());
        Assert.False(gate.TryComplete());
    }

    [Fact]
    public void Dayswork2Review_R6_TeardownRemovesOnlyTheTargetShiftsRegistrations()
    {
        var ownership = new ReferenceOwnershipIndex<object, string>();
        ownership.TryAdd(new object(), "shift-a");
        ownership.TryAdd(new object(), "shift-a");
        ownership.TryAdd(new object(), "shift-b");

        Assert.Equal(2, ownership.RemoveWhere(owner => owner == "shift-a"));
        Assert.Single(ownership.Snapshot(_ => true));
        Assert.Equal("shift-b", ownership.Snapshot(_ => true)[0].Value);
    }

    private sealed class EqualLookingTree
    {
        public override bool Equals(object? obj) => obj is EqualLookingTree;

        public override int GetHashCode() => 1;
    }
}
