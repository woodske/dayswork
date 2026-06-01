using Dayswork.Orchestration;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class WorkerActionPlayerStateSnapshotTests
{
    [Fact]
    public void Restore_PreservesActiveAnimationProgressWithoutRestartingAnimation()
    {
        var originalAnimation = Frames(12, 13, 14, 15);

        var playerState = new FakeWorkerActionPlayerState
        {
            CurrentAnimation = originalAnimation,
            CurrentAnimationIndex = 2,
            AnimationTimer = 37f,
            AnimationInterval = 81f,
            OldFrame = 4,
            CurrentFrame = 14,
            CurrentAnimationFrameCount = 4,
            CurrentSingleAnimationInterval = 81f,
            PauseForSingleAnimation = true,
            CurrentSingleAnimation = 160,
            LoopThisAnimation = false,
            AnimateBackwards = true,
            AnimatingBackwards = true,
            CurrentToolIndex = 96,
            OldAnimationInterval = 175f,
            UsingTool = true,
            CanMove = false,
            CanReleaseTool = true,
            JitterStrength = 0.5f,
            XVelocity = 1.25f,
            YVelocity = -0.75f,
        };

        var snapshot = WorkerActionPlayerStateSnapshot.Capture(playerState);

        playerState.CurrentAnimation = Frames(44);
        playerState.CurrentAnimationIndex = 0;
        playerState.AnimationTimer = 999f;
        playerState.AnimationInterval = 999f;
        playerState.OldFrame = 99;
        playerState.CurrentFrame = 44;
        playerState.CurrentAnimationFrameCount = 1;
        playerState.CurrentSingleAnimationInterval = 999f;
        playerState.PauseForSingleAnimation = false;
        playerState.CurrentSingleAnimation = 281;
        playerState.LoopThisAnimation = true;
        playerState.AnimateBackwards = false;
        playerState.AnimatingBackwards = false;
        playerState.CurrentToolIndex = 12;
        playerState.OldAnimationInterval = 20f;
        playerState.UsingTool = false;
        playerState.CanMove = true;
        playerState.CanReleaseTool = false;
        playerState.JitterStrength = 2f;
        playerState.XVelocity = 8f;
        playerState.YVelocity = 6f;

        snapshot.Restore(playerState);

        Assert.Equal(1, playerState.ResetTransientActionStateCalls);
        Assert.NotSame(originalAnimation, playerState.AnimationFrames);
        Assert.Equal(new[] { 12, 13, 14, 15 }, playerState.AnimationFrames!.Select(frame => frame.Frame));
        Assert.Equal(2, playerState.CurrentAnimationIndex);
        Assert.Equal(37f, playerState.AnimationTimer);
        Assert.Equal(81f, playerState.AnimationInterval);
        Assert.Equal(4, playerState.OldFrame);
        Assert.Equal(14, playerState.CurrentFrame);
        Assert.Equal(4, playerState.CurrentAnimationFrameCount);
        Assert.Equal(81f, playerState.CurrentSingleAnimationInterval);
        Assert.True(playerState.PauseForSingleAnimation);
        Assert.Equal(160, playerState.CurrentSingleAnimation);
        Assert.False(playerState.LoopThisAnimation);
        Assert.True(playerState.AnimateBackwards);
        Assert.True(playerState.AnimatingBackwards);
        Assert.Equal(96, playerState.CurrentToolIndex);
        Assert.Equal(175f, playerState.OldAnimationInterval);
        Assert.True(playerState.UsingTool);
        Assert.False(playerState.CanMove);
        Assert.True(playerState.CanReleaseTool);
        Assert.Equal(0.5f, playerState.JitterStrength);
        Assert.Equal(1.25f, playerState.XVelocity);
        Assert.Equal(-0.75f, playerState.YVelocity);
    }

    [Fact]
    public void Restore_ClearsWorkerInjectedAnimation_WhenNoAnimationWasSaved()
    {
        var playerState = new FakeWorkerActionPlayerState
        {
            CurrentAnimation = null,
            CurrentAnimationIndex = 0,
            AnimationTimer = 12f,
            AnimationInterval = 175f,
            OldFrame = 1,
            CurrentFrame = 14,
            CurrentAnimationFrameCount = 0,
            CurrentSingleAnimationInterval = 200f,
            PauseForSingleAnimation = false,
            CurrentSingleAnimation = -1,
            LoopThisAnimation = false,
            AnimateBackwards = false,
            AnimatingBackwards = false,
            CurrentToolIndex = 0,
            OldAnimationInterval = 175f,
            UsingTool = false,
            CanMove = true,
            CanReleaseTool = false,
            JitterStrength = 0f,
            XVelocity = 0f,
            YVelocity = 0f,
        };

        var snapshot = WorkerActionPlayerStateSnapshot.Capture(playerState);

        playerState.CurrentAnimation = Frames(44, 45);
        playerState.CurrentAnimationIndex = 1;
        playerState.AnimationTimer = 999f;
        playerState.PauseForSingleAnimation = true;
        playerState.CurrentSingleAnimation = 281;
        playerState.UsingTool = true;
        playerState.CanMove = false;
        playerState.CanReleaseTool = true;

        snapshot.Restore(playerState);

        Assert.Equal(1, playerState.ResetTransientActionStateCalls);
        Assert.True(playerState.ClearAnimationCalled);
        Assert.True(playerState.StopAnimationCalled);
        Assert.Null(playerState.AnimationFrames);
        Assert.Equal(0, playerState.CurrentAnimationIndex);
        Assert.Equal(12f, playerState.AnimationTimer);
        Assert.Equal(175f, playerState.AnimationInterval);
        Assert.Equal(14, playerState.CurrentFrame);
        Assert.False(playerState.PauseForSingleAnimation);
        Assert.Equal(-1, playerState.CurrentSingleAnimation);
        Assert.False(playerState.UsingTool);
        Assert.True(playerState.CanMove);
        Assert.False(playerState.CanReleaseTool);
    }

    [Property(Arbitrary = new[] { typeof(WorkerActionPlayerStateSnapshotGenerators) }, MaxTest = 300)]
    public bool Restore_ReturnsCapturedObservableState_AfterWorkerMutation(GeneratedPlayerActionState generated)
    {
        var playerState = ToFakeState(generated);
        var expected = PlayerStateObservation.Capture(playerState);
        var snapshot = WorkerActionPlayerStateSnapshot.Capture(playerState);

        ApplyWorkerMutation(playerState);

        snapshot.Restore(playerState);

        return expected == PlayerStateObservation.Capture(playerState);
    }

    private static List<WorkerActionAnimationFrame> Frames(params int[] frames) =>
        frames.Select((frame, index) => new WorkerActionAnimationFrame(frame, 20 + index, 0, 0, 0, false, null, null)).ToList();

    private static void ApplyWorkerMutation(FakeWorkerActionPlayerState playerState)
    {
        playerState.CurrentAnimation = Frames(280, 281);
        playerState.CurrentAnimationIndex = 0;
        playerState.AnimationTimer = 999f;
        playerState.AnimationInterval = 999f;
        playerState.OldFrame = 99;
        playerState.CurrentFrame = 281;
        playerState.CurrentAnimationFrameCount = 2;
        playerState.CurrentSingleAnimationInterval = 999f;
        playerState.PauseForSingleAnimation = true;
        playerState.CurrentSingleAnimation = 281;
        playerState.LoopThisAnimation = true;
        playerState.AnimateBackwards = !playerState.AnimateBackwards;
        playerState.AnimatingBackwards = !playerState.AnimatingBackwards;
        playerState.CurrentToolIndex = 255;
        playerState.OldAnimationInterval = 1f;
        playerState.UsingTool = !playerState.UsingTool;
        playerState.CanMove = !playerState.CanMove;
        playerState.CanReleaseTool = !playerState.CanReleaseTool;
        playerState.JitterStrength = 9f;
        playerState.XVelocity = 8f;
        playerState.YVelocity = 7f;
    }

    private static FakeWorkerActionPlayerState ToFakeState(GeneratedPlayerActionState generated) =>
        new()
        {
            CurrentAnimation = generated.AnimationFrames.Length == 0
                ? null
                : generated.AnimationFrames.Select((frame, index) => new WorkerActionAnimationFrame(frame, 25 + index, 0, 0, 0, false, null, null)).ToList(),
            CurrentAnimationIndex = generated.CurrentAnimationIndex,
            AnimationTimer = generated.AnimationTimer,
            AnimationInterval = generated.AnimationInterval,
            OldFrame = generated.OldFrame,
            CurrentFrame = generated.CurrentFrame,
            CurrentAnimationFrameCount = generated.CurrentAnimationFrameCount,
            CurrentSingleAnimationInterval = generated.CurrentSingleAnimationInterval,
            PauseForSingleAnimation = generated.PauseForSingleAnimation,
            CurrentSingleAnimation = generated.CurrentSingleAnimation,
            LoopThisAnimation = generated.LoopThisAnimation,
            AnimateBackwards = generated.AnimateBackwards,
            AnimatingBackwards = generated.AnimatingBackwards,
            CurrentToolIndex = generated.CurrentToolIndex,
            OldAnimationInterval = generated.OldAnimationInterval,
            UsingTool = generated.UsingTool,
            CanMove = generated.CanMove,
            CanReleaseTool = generated.CanReleaseTool,
            JitterStrength = generated.JitterStrength,
            XVelocity = generated.XVelocity,
            YVelocity = generated.YVelocity,
        };

    public sealed record GeneratedPlayerActionState(
        int[] AnimationFrames,
        int CurrentAnimationIndex,
        float AnimationTimer,
        float AnimationInterval,
        int OldFrame,
        int CurrentFrame,
        int CurrentAnimationFrameCount,
        float CurrentSingleAnimationInterval,
        bool PauseForSingleAnimation,
        int CurrentSingleAnimation,
        bool LoopThisAnimation,
        bool AnimateBackwards,
        bool AnimatingBackwards,
        int CurrentToolIndex,
        float OldAnimationInterval,
        bool UsingTool,
        bool CanMove,
        bool CanReleaseTool,
        float JitterStrength,
        float XVelocity,
        float YVelocity);

    public static class WorkerActionPlayerStateSnapshotGenerators
    {
        public static Arbitrary<GeneratedPlayerActionState> PlayerActionStates()
        {
            var gen =
                from hasAnimation in Arb.Generate<bool>()
                from animationCount in hasAnimation ? Gen.Choose(1, 5) : Gen.Constant(0)
                from animationFrames in AnimationFrames(animationCount)
                from currentAnimationIndex in animationCount > 0
                    ? Gen.Choose(0, animationCount - 1)
                    : Gen.Constant(0)
                from animationTimer in Milliseconds()
                from animationInterval in Milliseconds(1, 500)
                from oldFrame in Gen.Choose(0, 304)
                from currentFrame in Gen.Choose(0, 304)
                from currentSingleAnimationInterval in Milliseconds(1, 500)
                from pauseForSingleAnimation in Arb.Generate<bool>()
                from currentSingleAnimation in Gen.Choose(-1, 304)
                from loopThisAnimation in Arb.Generate<bool>()
                from animateBackwards in Arb.Generate<bool>()
                from animatingBackwards in Arb.Generate<bool>()
                from currentToolIndex in Gen.Choose(0, 500)
                from oldAnimationInterval in Milliseconds(1, 500)
                from usingTool in Arb.Generate<bool>()
                from canMove in Arb.Generate<bool>()
                from canReleaseTool in Arb.Generate<bool>()
                from jitterStrength in Tenths(0, 50)
                from xVelocity in Tenths(-50, 50)
                from yVelocity in Tenths(-50, 50)
                select new GeneratedPlayerActionState(
                    animationFrames,
                    currentAnimationIndex,
                    animationTimer,
                    animationInterval,
                    oldFrame,
                    currentFrame,
                    animationCount,
                    currentSingleAnimationInterval,
                    pauseForSingleAnimation,
                    currentSingleAnimation,
                    loopThisAnimation,
                    animateBackwards,
                    animatingBackwards,
                    currentToolIndex,
                    oldAnimationInterval,
                    usingTool,
                    canMove,
                    canReleaseTool,
                    jitterStrength,
                    xVelocity,
                    yVelocity);

            return gen.ToArbitrary();
        }

        private static Gen<int[]> AnimationFrames(int count)
        {
            if (count == 0)
                return Gen.Constant(Array.Empty<int>());

            return Gen.Sequence(Enumerable.Range(0, count).Select(_ => Gen.Choose(0, 304)))
                .Select(values => values.ToArray());
        }

        private static Gen<float> Milliseconds(int minimum = 0, int maximum = 1000) =>
            Gen.Choose(minimum, maximum).Select(value => (float)value);

        private static Gen<float> Tenths(int minimum, int maximum) =>
            Gen.Choose(minimum, maximum).Select(value => value / 10f);
    }

    private sealed record PlayerStateObservation(
        string AnimationDescription,
        int CurrentAnimationIndex,
        float AnimationTimer,
        float AnimationInterval,
        int OldFrame,
        int CurrentFrame,
        int CurrentAnimationFrameCount,
        float CurrentSingleAnimationInterval,
        bool PauseForSingleAnimation,
        int CurrentSingleAnimation,
        bool LoopThisAnimation,
        bool AnimateBackwards,
        bool AnimatingBackwards,
        int CurrentToolIndex,
        float? OldAnimationInterval,
        bool UsingTool,
        bool CanMove,
        bool CanReleaseTool,
        float JitterStrength,
        float XVelocity,
        float YVelocity)
    {
        public static PlayerStateObservation Capture(FakeWorkerActionPlayerState playerState) =>
            new(
                DescribeAnimation(playerState.AnimationFrames),
                playerState.CurrentAnimationIndex,
                playerState.AnimationTimer,
                playerState.AnimationInterval,
                playerState.OldFrame,
                playerState.CurrentFrame,
                playerState.CurrentAnimationFrameCount,
                playerState.CurrentSingleAnimationInterval,
                playerState.PauseForSingleAnimation,
                playerState.CurrentSingleAnimation,
                playerState.LoopThisAnimation,
                playerState.AnimateBackwards,
                playerState.AnimatingBackwards,
                playerState.CurrentToolIndex,
                playerState.OldAnimationInterval,
                playerState.UsingTool,
                playerState.CanMove,
                playerState.CanReleaseTool,
                playerState.JitterStrength,
                playerState.XVelocity,
                playerState.YVelocity);

        private static string DescribeAnimation(IReadOnlyList<WorkerActionAnimationFrame>? frames) =>
            frames is null
                ? "<none>"
                : string.Join("|", frames.Select(frame => $"{frame.Frame}:{frame.Milliseconds}:{frame.PositionOffset}:{frame.XOffset}:{frame.ArmOffset}:{frame.Flip}"));
    }

    private sealed class FakeWorkerActionPlayerState : IWorkerActionPlayerState
    {
        public List<WorkerActionAnimationFrame>? AnimationFrames { get; private set; }

        public IReadOnlyList<WorkerActionAnimationFrame>? CurrentAnimation
        {
            get => AnimationFrames?.ToList();
            set => AnimationFrames = value?.ToList();
        }

        public int CurrentAnimationIndex { get; set; }
        public float AnimationTimer { get; set; }
        public float AnimationInterval { get; set; }
        public int OldFrame { get; set; }
        public int CurrentFrame { get; set; }
        public int CurrentAnimationFrameCount { get; set; }
        public float CurrentSingleAnimationInterval { get; set; }
        public bool PauseForSingleAnimation { get; set; }
        public int CurrentSingleAnimation { get; set; }
        public bool LoopThisAnimation { get; set; }
        public bool AnimateBackwards { get; set; }
        public bool AnimatingBackwards { get; set; }
        public int CurrentToolIndex { get; set; }
        public object? EndOfAnimationFunction { get; set; }
        public float? OldAnimationInterval { get; set; }
        public bool UsingTool { get; set; }
        public bool CanMove { get; set; }
        public bool CanReleaseTool { get; set; }
        public float JitterStrength { get; set; }
        public float XVelocity { get; set; }
        public float YVelocity { get; set; }
        public int ResetTransientActionStateCalls { get; private set; }
        public bool ClearAnimationCalled { get; private set; }
        public bool StopAnimationCalled { get; private set; }

        public void ClearAnimation()
        {
            ClearAnimationCalled = true;
            AnimationFrames = null;
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
