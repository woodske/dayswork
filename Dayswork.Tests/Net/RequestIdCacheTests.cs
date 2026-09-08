namespace Dayswork.Tests.Net;

using Dayswork.Core.Net;
using Xunit;

/// <summary>
/// Idempotency for retried requests. The case that matters is a client whose 10-second timeout
/// fires on a commit the host <em>did</em> process: without this, the retry would charge a
/// one-time contract's price a second time.
/// </summary>
public class RequestIdCacheTests
{
    [Fact]
    public void AFreshIdIsNew()
    {
        var cache = new RequestIdCache();

        Assert.True(cache.TryBegin("a", out var previous));
        Assert.Null(previous);
    }

    [Fact]
    public void ARepeatedIdIsNotNew()
    {
        var cache = new RequestIdCache();
        cache.TryBegin("a", out _);

        Assert.False(cache.TryBegin("a", out _));
    }

    [Fact]
    public void ARetryReplaysTheOriginalAnswer()
    {
        var cache = new RequestIdCache();
        var response = new ContractCommitResponseMessage { RequestId = "a", Accepted = true, Revision = 3 };

        cache.TryBegin("a", out _);
        cache.Remember("a", response);

        Assert.False(cache.TryBegin("a", out var previous));
        Assert.Same(response, previous);
    }

    [Fact]
    public void ARetryOfSomethingNotYetAnsweredReplaysNothing()
    {
        // The first copy is still being processed (or crashed): there is no answer to replay, and
        // the caller must not act on a null as if it were an acceptance.
        var cache = new RequestIdCache();
        cache.TryBegin("a", out _);

        Assert.False(cache.TryBegin("a", out var previous));
        Assert.Null(previous);
    }

    [Fact]
    public void AnEmptyIdIsNeverDeduplicated()
    {
        // An unidentified request cannot be matched to a previous one, so it is always treated as
        // new rather than being silently swallowed as a duplicate of another blank id.
        var cache = new RequestIdCache();

        Assert.True(cache.TryBegin("", out _));
        Assert.True(cache.TryBegin("", out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void TheOldestIdsAreEvictedPastCapacity()
    {
        var cache = new RequestIdCache(capacity: 3);
        foreach (var id in new[] { "a", "b", "c", "d" })
            cache.TryBegin(id, out _);

        Assert.Equal(3, cache.Count);
        Assert.True(cache.TryBegin("a", out _));    // evicted, so it looks new again
        Assert.False(cache.TryBegin("d", out _));   // still remembered
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var cache = new RequestIdCache();
        cache.TryBegin("a", out _);
        cache.Remember("a", new ContractActionResponseMessage { RequestId = "a", Accepted = true });

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.True(cache.TryBegin("a", out var previous));
        Assert.Null(previous);
    }

    [Fact]
    public void CapacityMustBePositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestIdCache(capacity: 0));
}
