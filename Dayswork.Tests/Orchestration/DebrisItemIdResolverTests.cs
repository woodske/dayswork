using Dayswork.Core.Inventory;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class DebrisItemIdResolverTests
{
    [Theory]
    [InlineData("390", "(O)390")]
    [InlineData("(O)390", "(O)390")]
    [InlineData("(BC)99", "(BC)99")]
    public void TryNormalize_ResolvesExpectedCollectibleIds(string rawItemId, string expectedQualifiedItemId)
    {
        var resolved = CollectibleItemIdNormalizer.TryNormalize(rawItemId, out var qualifiedItemId);

        Assert.True(resolved);
        Assert.Equal(expectedQualifiedItemId, qualifiedItemId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("(O)")]
    [InlineData("(BC)")]
    public void TryNormalize_RejectsIncompleteIds(string rawItemId)
    {
        var resolved = CollectibleItemIdNormalizer.TryNormalize(rawItemId, out var qualifiedItemId);

        Assert.False(resolved);
        Assert.Equal(string.Empty, qualifiedItemId);
    }
}
