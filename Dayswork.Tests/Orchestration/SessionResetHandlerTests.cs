using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class SessionResetHandlerTests
{
    [Fact]
    public void OnSaveLoaded_ResetsRuntimeForSaveLoadedBoundary()
    {
        var resettable = new FakeResettable();
        var sut = new SessionResetHandler(resettable);

        sut.ResetForSaveLoaded();

        Assert.Equal(new[] { SessionResetBoundary.SaveLoaded }, resettable.Boundaries);
    }

    [Fact]
    public void OnReturnedToTitle_ResetsRuntimeForReturnedToTitleBoundary()
    {
        var resettable = new FakeResettable();
        var sut = new SessionResetHandler(resettable);

        sut.ResetForReturnedToTitle();

        Assert.Equal(new[] { SessionResetBoundary.ReturnedToTitle }, resettable.Boundaries);
    }

    private sealed class FakeResettable : ISessionBoundaryResettable
    {
        public List<SessionResetBoundary> Boundaries { get; } = new();

        public void ResetForSessionBoundary(SessionResetBoundary boundary) => Boundaries.Add(boundary);
    }
}
