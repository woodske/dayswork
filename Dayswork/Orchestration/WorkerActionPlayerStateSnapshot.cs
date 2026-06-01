using StardewValley;
using System.Reflection;

namespace Dayswork.Orchestration;

internal interface IWorkerActionPlayerState
{
    IReadOnlyList<WorkerActionAnimationFrame>? CurrentAnimation { get; set; }
    int CurrentAnimationIndex { get; set; }
    float AnimationTimer { get; set; }
    float AnimationInterval { get; set; }
    int OldFrame { get; set; }
    int CurrentFrame { get; set; }
    int CurrentAnimationFrameCount { get; set; }
    float CurrentSingleAnimationInterval { get; set; }
    bool PauseForSingleAnimation { get; set; }
    int CurrentSingleAnimation { get; set; }
    bool LoopThisAnimation { get; set; }
    bool AnimateBackwards { get; set; }
    bool AnimatingBackwards { get; set; }
    int CurrentToolIndex { get; set; }
    object? EndOfAnimationFunction { get; set; }
    float? OldAnimationInterval { get; set; }
    bool UsingTool { get; set; }
    bool CanMove { get; set; }
    bool CanReleaseTool { get; set; }
    float JitterStrength { get; set; }
    float XVelocity { get; set; }
    float YVelocity { get; set; }

    void ClearAnimation();
    void StopAnimation();
    void ResetTransientActionState();
}

internal readonly record struct WorkerActionAnimationFrame(
    int Frame,
    int Milliseconds,
    int PositionOffset,
    int XOffset,
    int ArmOffset,
    bool Flip,
    object? FrameStartBehavior,
    object? FrameEndBehavior);

internal sealed class WorkerActionPlayerStateSnapshot
{
    private readonly List<WorkerActionAnimationFrame>? _currentAnimation;
    private readonly int _currentAnimationIndex;
    private readonly float _animationTimer;
    private readonly float _animationInterval;
    private readonly int _oldFrame;
    private readonly int _currentFrame;
    private readonly int _currentAnimationFrameCount;
    private readonly float _currentSingleAnimationInterval;
    private readonly bool _pauseForSingleAnimation;
    private readonly int _currentSingleAnimation;
    private readonly bool _loopThisAnimation;
    private readonly bool _animateBackwards;
    private readonly bool _animatingBackwards;
    private readonly int _currentToolIndex;
    private readonly object? _endOfAnimationFunction;
    private readonly float? _oldAnimationInterval;
    private readonly bool _usingTool;
    private readonly bool _canMove;
    private readonly bool _canReleaseTool;
    private readonly float _jitterStrength;
    private readonly float _xVelocity;
    private readonly float _yVelocity;

    private WorkerActionPlayerStateSnapshot(
        List<WorkerActionAnimationFrame>? currentAnimation,
        int currentAnimationIndex,
        float animationTimer,
        float animationInterval,
        int oldFrame,
        int currentFrame,
        int currentAnimationFrameCount,
        float currentSingleAnimationInterval,
        bool pauseForSingleAnimation,
        int currentSingleAnimation,
        bool loopThisAnimation,
        bool animateBackwards,
        bool animatingBackwards,
        int currentToolIndex,
        object? endOfAnimationFunction,
        float? oldAnimationInterval,
        bool usingTool,
        bool canMove,
        bool canReleaseTool,
        float jitterStrength,
        float xVelocity,
        float yVelocity)
    {
        _currentAnimation = currentAnimation;
        _currentAnimationIndex = currentAnimationIndex;
        _animationTimer = animationTimer;
        _animationInterval = animationInterval;
        _oldFrame = oldFrame;
        _currentFrame = currentFrame;
        _currentAnimationFrameCount = currentAnimationFrameCount;
        _currentSingleAnimationInterval = currentSingleAnimationInterval;
        _pauseForSingleAnimation = pauseForSingleAnimation;
        _currentSingleAnimation = currentSingleAnimation;
        _loopThisAnimation = loopThisAnimation;
        _animateBackwards = animateBackwards;
        _animatingBackwards = animatingBackwards;
        _currentToolIndex = currentToolIndex;
        _endOfAnimationFunction = endOfAnimationFunction;
        _oldAnimationInterval = oldAnimationInterval;
        _usingTool = usingTool;
        _canMove = canMove;
        _canReleaseTool = canReleaseTool;
        _jitterStrength = jitterStrength;
        _xVelocity = xVelocity;
        _yVelocity = yVelocity;
    }

    public static WorkerActionPlayerStateSnapshot Capture(IWorkerActionPlayerState playerState) =>
        new(
            playerState.CurrentAnimation?.ToList(),
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
            playerState.EndOfAnimationFunction,
            playerState.OldAnimationInterval,
            playerState.UsingTool,
            playerState.CanMove,
            playerState.CanReleaseTool,
            playerState.JitterStrength,
            playerState.XVelocity,
            playerState.YVelocity);

    public void Restore(IWorkerActionPlayerState playerState)
    {
        playerState.ResetTransientActionState();

        if (_currentAnimation is not null)
            playerState.CurrentAnimation = _currentAnimation.ToList();
        else
        {
            playerState.ClearAnimation();
            playerState.StopAnimation();
        }

        playerState.CurrentAnimationIndex = _currentAnimationIndex;
        playerState.AnimationTimer = _animationTimer;
        playerState.AnimationInterval = _animationInterval;
        playerState.OldFrame = _oldFrame;
        playerState.CurrentFrame = _currentFrame;
        playerState.CurrentAnimationFrameCount = _currentAnimationFrameCount;
        playerState.CurrentSingleAnimationInterval = _currentSingleAnimationInterval;
        playerState.PauseForSingleAnimation = _pauseForSingleAnimation;
        playerState.CurrentSingleAnimation = _currentSingleAnimation;
        playerState.LoopThisAnimation = _loopThisAnimation;
        playerState.AnimateBackwards = _animateBackwards;
        playerState.AnimatingBackwards = _animatingBackwards;
        playerState.CurrentToolIndex = _currentToolIndex;
        playerState.EndOfAnimationFunction = _endOfAnimationFunction;
        playerState.OldAnimationInterval = _oldAnimationInterval;
        playerState.UsingTool = _usingTool;
        playerState.CanMove = _canMove;
        playerState.CanReleaseTool = _canReleaseTool;
        playerState.JitterStrength = _jitterStrength;
        playerState.XVelocity = _xVelocity;
        playerState.YVelocity = _yVelocity;
    }

    public bool DiffersFrom(IWorkerActionPlayerState playerState) => !Matches(Capture(playerState));

    public string Describe() =>
        string.Join(
            "|",
            $"frame={_currentFrame}",
            $"single={_currentSingleAnimation}",
            $"pause={_pauseForSingleAnimation}",
            $"anim={DescribeAnimation(_currentAnimation)}",
            $"idx={_currentAnimationIndex}",
            $"timer={_animationTimer:0.##}",
            $"interval={_animationInterval:0.##}",
            $"oldFrame={_oldFrame}",
            $"count={_currentAnimationFrameCount}",
            $"singleInterval={_currentSingleAnimationInterval:0.##}",
            $"loop={_loopThisAnimation}",
            $"backwards={_animateBackwards}/{_animatingBackwards}",
            $"toolIndex={_currentToolIndex}",
            $"usingTool={_usingTool}",
            $"canMove={_canMove}",
            $"canRelease={_canReleaseTool}",
            $"jitter={_jitterStrength:0.##}",
            $"velocity=({_xVelocity:0.##},{_yVelocity:0.##})");

    public static string Describe(IWorkerActionPlayerState playerState) => Capture(playerState).Describe();

    private bool Matches(WorkerActionPlayerStateSnapshot other) =>
        AnimationEquals(_currentAnimation, other._currentAnimation) &&
        _currentAnimationIndex == other._currentAnimationIndex &&
        _animationTimer.Equals(other._animationTimer) &&
        _animationInterval.Equals(other._animationInterval) &&
        _oldFrame == other._oldFrame &&
        _currentFrame == other._currentFrame &&
        _currentAnimationFrameCount == other._currentAnimationFrameCount &&
        _currentSingleAnimationInterval.Equals(other._currentSingleAnimationInterval) &&
        _pauseForSingleAnimation == other._pauseForSingleAnimation &&
        _currentSingleAnimation == other._currentSingleAnimation &&
        _loopThisAnimation == other._loopThisAnimation &&
        _animateBackwards == other._animateBackwards &&
        _animatingBackwards == other._animatingBackwards &&
        _currentToolIndex == other._currentToolIndex &&
        ReferenceEquals(_endOfAnimationFunction, other._endOfAnimationFunction) &&
        Nullable.Equals(_oldAnimationInterval, other._oldAnimationInterval) &&
        _usingTool == other._usingTool &&
        _canMove == other._canMove &&
        _canReleaseTool == other._canReleaseTool &&
        _jitterStrength.Equals(other._jitterStrength) &&
        _xVelocity.Equals(other._xVelocity) &&
        _yVelocity.Equals(other._yVelocity);

    private static string DescribeAnimation(IReadOnlyList<WorkerActionAnimationFrame>? animation)
    {
        if (animation is null)
            return "<none>";

        return "frames" + animation.Count + "=" + string.Join(",", animation.Select(frame => frame.Frame));
    }

    private static bool AnimationEquals(
        IReadOnlyList<WorkerActionAnimationFrame>? left,
        IReadOnlyList<WorkerActionAnimationFrame>? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!FrameEquals(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool FrameEquals(WorkerActionAnimationFrame left, WorkerActionAnimationFrame right) =>
        left.Frame == right.Frame &&
        left.Milliseconds == right.Milliseconds &&
        left.PositionOffset == right.PositionOffset &&
        left.XOffset == right.XOffset &&
        left.ArmOffset == right.ArmOffset &&
        left.Flip == right.Flip &&
        ReferenceEquals(left.FrameStartBehavior, right.FrameStartBehavior) &&
        ReferenceEquals(left.FrameEndBehavior, right.FrameEndBehavior);
}

internal sealed class Game1WorkerActionPlayerState : IWorkerActionPlayerState
{
    private static readonly FieldInfo? OldIntervalField = typeof(FarmerSprite)
        .GetField("oldInterval", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly Farmer _player;
    private readonly FarmerSprite _sprite;

    public Game1WorkerActionPlayerState(Farmer player)
    {
        _player = player;
        _sprite = player.FarmerSprite;
    }

    public IReadOnlyList<WorkerActionAnimationFrame>? CurrentAnimation
    {
        get => _sprite.CurrentAnimation?.Select(ToSnapshotFrame).ToList();
        set => _sprite.CurrentAnimation = value?.Select(ToGameFrame).ToList();
    }

    public int CurrentAnimationIndex
    {
        get => _sprite.currentAnimationIndex;
        set => _sprite.currentAnimationIndex = value;
    }

    public float AnimationTimer
    {
        get => _sprite.timer;
        set => _sprite.timer = value;
    }

    public float AnimationInterval
    {
        get => _sprite.interval;
        set => _sprite.interval = value;
    }

    public int OldFrame
    {
        get => _sprite.oldFrame;
        set => _sprite.oldFrame = value;
    }

    public int CurrentFrame
    {
        get => _sprite.CurrentFrame;
        set => _sprite.CurrentFrame = value;
    }

    public int CurrentAnimationFrameCount
    {
        get => _sprite.currentAnimationFrames;
        set => _sprite.currentAnimationFrames = value;
    }

    public float CurrentSingleAnimationInterval
    {
        get => _sprite.currentSingleAnimationInterval;
        set => _sprite.currentSingleAnimationInterval = value;
    }

    public bool PauseForSingleAnimation
    {
        get => _sprite.pauseForSingleAnimation;
        set => _sprite.pauseForSingleAnimation = value;
    }

    public int CurrentSingleAnimation
    {
        get => _sprite.currentSingleAnimation;
        set => _sprite.currentSingleAnimation = value;
    }

    public bool LoopThisAnimation
    {
        get => _sprite.loopThisAnimation;
        set => _sprite.loopThisAnimation = value;
    }

    public bool AnimateBackwards
    {
        get => _sprite.animateBackwards;
        set => _sprite.animateBackwards = value;
    }

    public bool AnimatingBackwards
    {
        get => _sprite.animatingBackwards;
        set => _sprite.animatingBackwards = value;
    }

    public int CurrentToolIndex
    {
        get => _sprite.CurrentToolIndex;
        set => _sprite.CurrentToolIndex = value;
    }

    public object? EndOfAnimationFunction
    {
        get => _sprite.endOfAnimationFunction;
        set => _sprite.endOfAnimationFunction = value as AnimatedSprite.endOfAnimationBehavior;
    }

    public float? OldAnimationInterval
    {
        get => OldIntervalField?.GetValue(_sprite) is float value ? value : null;
        set
        {
            if (value.HasValue)
                OldIntervalField?.SetValue(_sprite, value.Value);
        }
    }

    public bool UsingTool
    {
        get => _player.UsingTool;
        set => _player.UsingTool = value;
    }

    public bool CanMove
    {
        get => _player.CanMove;
        set => _player.CanMove = value;
    }

    public bool CanReleaseTool
    {
        get => _player.canReleaseTool;
        set => _player.canReleaseTool = value;
    }

    public float JitterStrength
    {
        get => _player.jitterStrength;
        set => _player.jitterStrength = value;
    }

    public float XVelocity
    {
        get => _player.xVelocity;
        set => _player.xVelocity = value;
    }

    public float YVelocity
    {
        get => _player.yVelocity;
        set => _player.yVelocity = value;
    }

    public void ClearAnimation() => _sprite.ClearAnimation();

    public void StopAnimation() => _sprite.StopAnimation();

    public void ResetTransientActionState()
    {
        _sprite.pauseForSingleAnimation = false;
        _player.UsingTool = false;
        _player.canReleaseTool = false;
        _player.jitterStrength = 0f;
        _player.xVelocity = 0f;
        _player.yVelocity = 0f;
    }

    private static WorkerActionAnimationFrame ToSnapshotFrame(FarmerSprite.AnimationFrame frame) =>
        new(
            frame.frame,
            frame.milliseconds,
            frame.positionOffset,
            frame.xOffset,
            frame.armOffset,
            frame.flip,
            frame.frameStartBehavior,
            frame.frameEndBehavior);

    private static FarmerSprite.AnimationFrame ToGameFrame(WorkerActionAnimationFrame frame) =>
        new(
            frame.Frame,
            frame.Milliseconds,
            frame.PositionOffset,
            frame.ArmOffset,
            frame.Flip,
            frame.FrameStartBehavior as AnimatedSprite.endOfAnimationBehavior,
            frame.FrameEndBehavior as AnimatedSprite.endOfAnimationBehavior,
            frame.XOffset);
}
