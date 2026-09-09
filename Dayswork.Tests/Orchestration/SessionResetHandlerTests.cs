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

        sut.ResetForSaveLoaded(isHostScreen: true);

        Assert.Equal(new[] { SessionResetBoundary.SaveLoaded }, resettable.Boundaries);
    }

    [Fact]
    public void OnReturnedToTitle_ResetsRuntimeForReturnedToTitleBoundary()
    {
        var resettable = new FakeResettable();
        var sut = new SessionResetHandler(resettable);

        sut.ResetForReturnedToTitle(isHostScreen: true);

        Assert.Equal(new[] { SessionResetBoundary.ReturnedToTitle }, resettable.Boundaries);
    }

    [Fact]
    public void Dayswork2Review_R1_GuestLifecycleDoesNotResetSharedHostRuntime()
    {
        var resettable = new FakeResettable();
        var sut = new SessionResetHandler(resettable);

        sut.ResetForSaveLoaded(isHostScreen: true);
        resettable.StartShiftWithBufferedOutput();
        sut.ResetForSaveLoaded(isHostScreen: false);
        sut.ResetForReturnedToTitle(isHostScreen: false);

        Assert.Equal(new[] { SessionResetBoundary.SaveLoaded }, resettable.Boundaries);
        Assert.True(resettable.HasWorker);
        Assert.Equal(3, resettable.BufferedItems);

        sut.ResetForReturnedToTitle(isHostScreen: true);

        Assert.Equal(
            new[] { SessionResetBoundary.SaveLoaded, SessionResetBoundary.ReturnedToTitle },
            resettable.Boundaries);
        Assert.False(resettable.HasWorker);
        Assert.Equal(0, resettable.BufferedItems);
    }

    private sealed class FakeResettable : ISessionBoundaryResettable
    {
        public List<SessionResetBoundary> Boundaries { get; } = new();

        public bool HasWorker { get; private set; }

        public int BufferedItems { get; private set; }

        public void StartShiftWithBufferedOutput()
        {
            HasWorker = true;
            BufferedItems = 3;
        }

        public void ResetForSessionBoundary(SessionResetBoundary boundary)
        {
            Boundaries.Add(boundary);
            HasWorker = false;
            BufferedItems = 0;
        }
    }
}
