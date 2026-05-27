using StardewValley;

namespace Dayswork.Orchestration;

internal interface IWorkerActionPlayerState
{
    int CurrentFrame { get; set; }
    bool PauseForSingleAnimation { get; set; }
    int CurrentSingleAnimation { get; set; }
    bool UsingTool { get; set; }
    bool CanMove { get; set; }
    bool CanReleaseTool { get; set; }
    float JitterStrength { get; set; }
    float XVelocity { get; set; }
    float YVelocity { get; set; }

    object? CaptureAnimationToken();
    void RestoreAnimationToken(object animationToken);
    void ClearAnimation();
    void StopAnimation();
    void ResetTransientActionState();
}

internal sealed class WorkerActionPlayerStateSnapshot
{
    private readonly object? _animationToken;
    private readonly int _currentFrame;
    private readonly bool _pauseForSingleAnimation;
    private readonly int _currentSingleAnimation;
    private readonly bool _usingTool;
    private readonly bool _canMove;
    private readonly bool _canReleaseTool;
    private readonly float _jitterStrength;
    private readonly float _xVelocity;
    private readonly float _yVelocity;

    private WorkerActionPlayerStateSnapshot(
        object? animationToken,
        int currentFrame,
        bool pauseForSingleAnimation,
        int currentSingleAnimation,
        bool usingTool,
        bool canMove,
        bool canReleaseTool,
        float jitterStrength,
        float xVelocity,
        float yVelocity)
    {
        _animationToken = animationToken;
        _currentFrame = currentFrame;
        _pauseForSingleAnimation = pauseForSingleAnimation;
        _currentSingleAnimation = currentSingleAnimation;
        _usingTool = usingTool;
        _canMove = canMove;
        _canReleaseTool = canReleaseTool;
        _jitterStrength = jitterStrength;
        _xVelocity = xVelocity;
        _yVelocity = yVelocity;
    }

    public static WorkerActionPlayerStateSnapshot Capture(IWorkerActionPlayerState playerState) =>
        new(
            playerState.CaptureAnimationToken(),
            playerState.CurrentFrame,
            playerState.PauseForSingleAnimation,
            playerState.CurrentSingleAnimation,
            playerState.UsingTool,
            playerState.CanMove,
            playerState.CanReleaseTool,
            playerState.JitterStrength,
            playerState.XVelocity,
            playerState.YVelocity);

    public void Restore(IWorkerActionPlayerState playerState)
    {
        playerState.ResetTransientActionState();
        playerState.CurrentSingleAnimation = _currentSingleAnimation;

        if (_animationToken is not null)
            playerState.RestoreAnimationToken(_animationToken);
        else
        {
            playerState.ClearAnimation();
            playerState.StopAnimation();
        }

        playerState.CurrentFrame = _currentFrame;
        playerState.PauseForSingleAnimation = _pauseForSingleAnimation;
        playerState.UsingTool = _usingTool;
        playerState.CanMove = _canMove;
        playerState.CanReleaseTool = _canReleaseTool;
        playerState.JitterStrength = _jitterStrength;
        playerState.XVelocity = _xVelocity;
        playerState.YVelocity = _yVelocity;
    }
}

internal sealed class Game1WorkerActionPlayerState : IWorkerActionPlayerState
{
    private readonly Farmer _player;
    private readonly FarmerSprite _sprite;

    public Game1WorkerActionPlayerState(Farmer player)
    {
        _player = player;
        _sprite = player.FarmerSprite;
    }

    public int CurrentFrame
    {
        get => _sprite.CurrentFrame;
        set => _sprite.CurrentFrame = value;
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

    public object? CaptureAnimationToken() => _sprite.currentAnimation?.ToList();

    public void RestoreAnimationToken(object animationToken) => _sprite.setCurrentAnimation(((List<FarmerSprite.AnimationFrame>)animationToken).ToList());

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
}
