using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.Worker;

internal sealed class ToolSwapAnimator
{
    private FarmhandNpc? _worker;
    private double _swingMsRemaining;
    private double _workAnimationMs = 650d;

    public bool IsSwinging => _swingMsRemaining > 0;

    public void SetPacingProfile(WorkerPacingProfile profile)
    {
        _workAnimationMs = profile.ActionAnimationMs;
    }

    public void SetWorker(FarmhandNpc? worker)
    {
        _worker = worker;
        _swingMsRemaining = 0;
        worker?.StopTaskAnimation();
    }

    public void OnTaskChanged(TaskKind previous, TaskKind next) { }

    public void Update(GameTime time)
    {
        if (_worker is null || _swingMsRemaining <= 0)
            return;

        _worker.Sprite.animateOnce(time);
        _swingMsRemaining -= time.ElapsedGameTime.TotalMilliseconds;

        if (_swingMsRemaining <= 0)
            StopSwing();
    }

    public void PlaySwing(TaskKind task, int facingDirection)
    {
        if (_worker is null)
            return;

        _worker.FaceTaskDirection(facingDirection);
        _worker.Sprite.setCurrentAnimation(WorkFramesForTask(task, facingDirection));
        SpawnToolSwing(WorkerToolExtensions.ForTask(task), facingDirection);
        _swingMsRemaining = _workAnimationMs;
    }

    public void PlaySwing(WorkerTool tool, int facingDirection)
    {
        if (_worker is null)
            return;

        _worker.FaceTaskDirection(facingDirection);
        _worker.Sprite.setCurrentAnimation(WorkFramesFor(facingDirection));
        SpawnToolSwing(tool, facingDirection);
        _swingMsRemaining = _workAnimationMs;
    }

    public void StopSwing()
    {
        _swingMsRemaining = 0;
        _worker?.StopTaskAnimation();
    }

    // Harvest/collect/feed tasks use the alternate action frame (row*4+3) so they look
    // visually distinct from tool-swing tasks (row*4+1). All frame durations are derived
    // from _workAnimationMs so the animation plays exactly once per beat — no looping.
    private List<FarmerSprite.AnimationFrame> WorkFramesForTask(TaskKind task, int facingDirection)
    {
        var useAltPose = task is TaskKind.HarvestCrops or TaskKind.CollectFruit or TaskKind.FeedAnimals;
        var (actionFrame, idleFrame) = ActionIdleFrames(facingDirection, useAltPose);
        return BuildFrames(actionFrame, idleFrame);
    }

    private List<FarmerSprite.AnimationFrame> WorkFramesFor(int facingDirection)
    {
        var (actionFrame, idleFrame) = ActionIdleFrames(facingDirection, useAltPose: false);
        return BuildFrames(actionFrame, idleFrame);
    }

    // Returns the action and idle sprite frame indices for a given facing direction.
    // Row layout: 0(up)→row2, 1(right)→row1, 2(down)→row0, 3(left)→row3.
    // Generic action = row*4+1; alt-pose action = row*4+3 (second walk step).
    private static (int actionFrame, int idleFrame) ActionIdleFrames(int facingDirection, bool useAltPose)
    {
        var row = facingDirection switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            3 => 3,
            _ => 0,
        };
        var idleFrame   = row * 4;
        var actionFrame = useAltPose ? idleFrame + 3 : idleFrame + 1;
        return (actionFrame, idleFrame);
    }

    // Calculates frame durations from _workAnimationMs so the animation spans exactly
    // one config-controlled beat. The action frame is capped at 150 ms; idle fills the rest.
    private List<FarmerSprite.AnimationFrame> BuildFrames(int actionFrame, int idleFrame)
    {
        var totalMs  = (int)_workAnimationMs;
        var actionMs = Math.Max(1, Math.Min(150, totalMs - 1));
        var idleMs   = totalMs - actionMs;

        var frames = new List<FarmerSprite.AnimationFrame>
        {
            new FarmerSprite.AnimationFrame(actionFrame, actionMs),
        };
        if (idleMs > 0)
            frames.Add(new FarmerSprite.AnimationFrame(idleFrame, idleMs));
        return frames;
    }

    private void SpawnToolSwing(WorkerTool tool, int facingDirection)
    {
        if (_worker is null || _worker.currentLocation is null)
            return;

        switch (tool)
        {
            case WorkerTool.Hoe:
                SpawnHeavyToolSwing(facingDirection, toolSpriteSheetY: 16);
                // makeHoeDirt is called directly (not via Hoe.DoFunction), so the vanilla
                // "hoeHit" sound never fires — emit it here.
                _worker.currentLocation.playSound("hoeHit", _worker.Tile);
                break;
            case WorkerTool.Pickaxe:
                SpawnHeavyToolSwing(facingDirection, toolSpriteSheetY: 80);
                // The rock clear path calls Object.performToolAction directly rather than
                // Pickaxe.DoFunction, so the vanilla hit sound never fires — emit it here.
                _worker.currentLocation.playSound("hammer", _worker.Tile);
                break;
            case WorkerTool.Axe:
                SpawnHeavyToolSwing(facingDirection, toolSpriteSheetY: 144);
                break;
            case WorkerTool.WateringCan:
                SpawnWateringCanSwing(facingDirection);
                break;
            case WorkerTool.Scythe:
                SpawnSwipeOverlay(facingDirection);
                break;
            case WorkerTool.MilkPail:
                SpawnToolIconOverlay(facingDirection, spriteIndex: 6);
                break;
            case WorkerTool.Shears:
                SpawnToolIconOverlay(facingDirection, spriteIndex: 7);
                break;
        }
    }

    private void SpawnHeavyToolSwing(int facingDirection, int toolSpriteSheetY)
    {
        if (_worker is null)
            return;

        switch (facingDirection)
        {
            case 1:
                BroadcastSprites(
                    ToolSprite(new Rectangle(32, toolSpriteSheetY, 16, 32), 75f, 1, _worker.Position + new Vector2(16f, -103f), flipped: false, rotation: 0f),
                    Delayed(ToolSprite(new Rectangle(32, toolSpriteSheetY, 16, 32), 325f, 1, _worker.Position + new Vector2(64f, -48f), flipped: false, rotation: MathHelper.PiOver2), 75));
                break;
            case 3:
                BroadcastSprites(
                    ToolSprite(new Rectangle(32, toolSpriteSheetY, 16, 32), 75f, 1, _worker.Position + new Vector2(-16f, -103f), flipped: true, rotation: 0f),
                    Delayed(ToolSprite(new Rectangle(32, toolSpriteSheetY, 16, 32), 325f, 1, _worker.Position + new Vector2(-64f, -48f), flipped: true, rotation: -MathHelper.PiOver2), 75));
                break;
            case 0:
                BroadcastSprites(ToolSprite(new Rectangle(48, toolSpriteSheetY, 16, 32), 200f, 2, _worker.Position + new Vector2(0f, -128f), flipped: false, rotation: 0f));
                break;
            default:
                BroadcastSprites(ToolSprite(new Rectangle(0, toolSpriteSheetY, 16, 32), 200f, 2, _worker.Position + new Vector2(0f, -80f), flipped: false, rotation: 0f));
                break;
        }
    }

    private void SpawnWateringCanSwing(int facingDirection)
    {
        if (_worker is null)
            return;

        var (sourceRect, offset, interval, frames, flipped) = facingDirection switch
        {
            0 => (new Rectangle(0, 208, 16, 32), new Vector2(0f, -32f), 400f, 1, false),
            1 => (new Rectangle(32, 208, 16, 32), new Vector2(48f, -64f), 200f, 2, false),
            3 => (new Rectangle(32, 208, 16, 32), new Vector2(-32f, -64f), 200f, 2, true),
            _ => (new Rectangle(64, 208, 16, 32), new Vector2(0f, -96f), 200f, 2, false),
        };

        BroadcastSprites(ToolSprite(sourceRect, interval, frames, _worker.Position + offset, flipped, rotation: 0f));
    }

    private void SpawnSwipeOverlay(int facingDirection)
    {
        if (_worker is null)
            return;

        var (sourceRect, offset, flipped, interval, layerDepth) = facingDirection switch
        {
            0 => (new Rectangle(0, 1152, 64, 64), new Vector2(0f, -100f), false, 50f, (_worker.StandingPixel.Y - 9f) / 10000f),
            1 => (new Rectangle(0, 960, 128, 128), new Vector2(20f, -100f), false, 40f, LayerDepthFor(_worker, aboveWhenFacingUp: false)),
            3 => (new Rectangle(0, 960, 128, 128), new Vector2(-92f, -100f), true, 40f, LayerDepthFor(_worker, aboveWhenFacingUp: false)),
            _ => (new Rectangle(0, 1216, 128, 128), new Vector2(-4f, -96f), false, 40f, LayerDepthFor(_worker, aboveWhenFacingUp: false)),
        };

        BroadcastSprites(Delayed(new TemporaryAnimatedSprite(
            "TileSheets\\animations",
            sourceRect,
            interval,
            4,
            0,
            _worker.Position + offset,
            flicker: false,
            flipped,
            layerDepth,
            alphaFade: 0f,
            Color.White,
            scale: 1f,
            scaleChange: 0f,
            rotation: 0f,
            rotationChange: 0f,
            local: false), 75));
    }

    // Milk pail and shears have no swing animation in vanilla SDV — they use only the farmer
    // body animation. We display the tool's inventory icon (16×16, scaled 4×) hovering near
    // the worker so the player can tell which task is being performed.
    // Tool sprite sheet layout: index N → X = (N*16) % sheetWidth, Y = (N*16/sheetWidth)*16.
    // For indices 6 (MilkPail) and 7 (Shears) the sheet is ≥128 px wide, so both land in row 0:
    //   MilkPail → (96, 0, 16, 16),  Shears → (112, 0, 16, 16)
    private void SpawnToolIconOverlay(int facingDirection, int spriteIndex)
    {
        if (_worker is null)
            return;

        var srcX = spriteIndex * 16 % 256; // 256 is a safe upper-bound; actual width is ≥128
        const int srcY = 0;
        var srcRect = new Rectangle(srcX, srcY, 16, 16);

        var (offset, flipped) = facingDirection switch
        {
            0 => (new Vector2(0f, -96f), false),
            1 => (new Vector2(48f, -64f), false),
            3 => (new Vector2(-32f, -64f), true),
            _ => (new Vector2(16f, -64f), false),
        };

        BroadcastSprites(ToolSprite(srcRect, 400f, 1, _worker.Position + offset, flipped, rotation: 0f));
    }

    private TemporaryAnimatedSprite ToolSprite(
        Rectangle sourceRect,
        float interval,
        int frames,
        Vector2 position,
        bool flipped,
        float rotation) =>
        new(
            Game1.toolSpriteSheet.Name,
            sourceRect,
            interval,
            frames,
            0,
            position,
            flicker: false,
            flipped,
            LayerDepthFor(_worker!, aboveWhenFacingUp: true),
            alphaFade: 0f,
            Color.White,
            scale: 4f,
            scaleChange: 0f,
            rotation,
            rotationChange: 0f,
            local: false);

    private static TemporaryAnimatedSprite Delayed(TemporaryAnimatedSprite sprite, int delay)
    {
        sprite.delayBeforeAnimationStart = delay;
        return sprite;
    }

    private void BroadcastSprites(params TemporaryAnimatedSprite[] sprites)
    {
        if (_worker?.currentLocation is not null)
            Game1.Multiplayer.broadcastSprites(_worker.currentLocation, sprites);
    }

    private static float LayerDepthFor(FarmhandNpc worker, bool aboveWhenFacingUp)
    {
        var bottom = worker.GetBoundingBox().Bottom;
        if (aboveWhenFacingUp && worker.FacingDirection == 0)
            return (bottom - 32f) / 10000f;

        return (bottom + 2f) / 10000f;
    }
}
