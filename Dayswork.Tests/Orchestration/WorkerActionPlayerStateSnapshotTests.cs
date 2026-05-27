using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class WorkerActionPlayerStateSnapshotTests
{
    [Fact]
    public void Restore_ReappliesSavedAnimationAndTransientPlayerFlags()
    {
        var originalAnimation = new List<int> { 12, 13 };

        var playerState = new FakeWorkerActionPlayerState
        {
            AnimationToken = originalAnimation,
            CurrentFrame = 77,
            PauseForSingleAnimation = true,
            CurrentSingleAnimation = 5,
            UsingTool = true,
            CanMove = false,
            CanReleaseTool = true,
            JitterStrength = 0.5f,
            XVelocity = 1.25f,
            YVelocity = -0.75f,
        };

        var snapshot = WorkerActionPlayerStateSnapshot.Capture(playerState);

        playerState.AnimationToken = new List<int> { 22 };
        playerState.CurrentFrame = 99;
        playerState.PauseForSingleAnimation = false;
        playerState.CurrentSingleAnimation = 11;
        playerState.UsingTool = false;
        playerState.CanMove = true;
        playerState.CanReleaseTool = false;
        playerState.JitterStrength = 2f;
        playerState.XVelocity = 8f;
        playerState.YVelocity = 6f;

        snapshot.Restore(playerState);

        Assert.Equal(1, playerState.ResetTransientActionStateCalls);
        Assert.Equal(2, Assert.IsAssignableFrom<List<int>>(playerState.AnimationToken).Count);
        Assert.NotSame(originalAnimation, playerState.AnimationToken);
        Assert.Equal(77, playerState.CurrentFrame);
        Assert.True(playerState.PauseForSingleAnimation);
        Assert.Equal(5, playerState.CurrentSingleAnimation);
        Assert.True(playerState.UsingTool);
        Assert.False(playerState.CanMove);
        Assert.True(playerState.CanReleaseTool);
        Assert.Equal(0.5f, playerState.JitterStrength);
        Assert.Equal(1.25f, playerState.XVelocity);
        Assert.Equal(-0.75f, playerState.YVelocity);
    }

    [Fact]
    public void Restore_ClearsAnimation_WhenNoAnimationWasSaved()
    {
        var playerState = new FakeWorkerActionPlayerState
        {
            AnimationToken = null,
            CurrentFrame = 14,
            PauseForSingleAnimation = false,
            CurrentSingleAnimation = 0,
            UsingTool = false,
            CanMove = true,
            CanReleaseTool = false,
            JitterStrength = 0f,
            XVelocity = 0f,
            YVelocity = 0f,
        };

        var snapshot = WorkerActionPlayerStateSnapshot.Capture(playerState);

        playerState.AnimationToken = new List<int> { 44 };

        snapshot.Restore(playerState);

        Assert.True(playerState.ClearAnimationCalled);
        Assert.True(playerState.StopAnimationCalled);
        Assert.Null(playerState.AnimationToken);
    }

    private sealed class FakeWorkerActionPlayerState : IWorkerActionPlayerState
    {
        public List<int>? AnimationToken { get; set; }
        public int CurrentFrame { get; set; }
        public bool PauseForSingleAnimation { get; set; }
        public int CurrentSingleAnimation { get; set; }
        public bool UsingTool { get; set; }
        public bool CanMove { get; set; }
        public bool CanReleaseTool { get; set; }
        public float JitterStrength { get; set; }
        public float XVelocity { get; set; }
        public float YVelocity { get; set; }
        public int ResetTransientActionStateCalls { get; private set; }
        public bool ClearAnimationCalled { get; private set; }
        public bool StopAnimationCalled { get; private set; }

        public object? CaptureAnimationToken() => AnimationToken?.ToList();

        public void RestoreAnimationToken(object animationToken) => AnimationToken = ((List<int>)animationToken).ToList();

        public void ClearAnimation()
        {
            ClearAnimationCalled = true;
            AnimationToken = null;
        }

        public void StopAnimation() => StopAnimationCalled = true;

        public void ResetTransientActionState()
        {
            ResetTransientActionStateCalls++;
            PauseForSingleAnimation = false;
            UsingTool = false;
            CanReleaseTool = false;
            JitterStrength = 0f;
            XVelocity = 0f;
            YVelocity = 0f;
        }
    }
}
