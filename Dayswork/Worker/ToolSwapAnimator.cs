using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.Worker;

internal sealed class ToolSwapAnimator
{
    private const double WorkAnimationMs = 400d;

    private FarmhandNpc? _worker;
    private double _swingMsRemaining;

    public bool IsSwinging => _swingMsRemaining > 0;

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
        _worker.Sprite.setCurrentAnimation(WorkFramesFor(facingDirection));
        SpawnToolSwing(WorkerToolExtensions.ForTask(task), facingDirection);
        _swingMsRemaining = WorkAnimationMs;
    }

    public void StopSwing()
    {
        _swingMsRemaining = 0;
        _worker?.StopTaskAnimation();
    }

    // Mirrors the generic Stardew Squad-style work beat: a brief action pose followed
    // by the idle frame in the same direction. It keeps task visuals readable without
    // invoking vanilla Farmer tool callbacks.
    private static List<FarmerSprite.AnimationFrame> WorkFramesFor(int facingDirection) =>
        facingDirection switch
        {
            0 => Frames(9, 8),
            1 => Frames(5, 4),
            2 => Frames(1, 0),
            3 => Frames(13, 12),
            _ => Frames(1, 0),
        };

    private static List<FarmerSprite.AnimationFrame> Frames(int actionFrame, int idleFrame) =>
        new()
        {
            new FarmerSprite.AnimationFrame(actionFrame, 150),
            new FarmerSprite.AnimationFrame(idleFrame, 250),
        };

    private void SpawnToolSwing(WorkerTool tool, int facingDirection)
    {
        if (_worker is null || _worker.currentLocation is null)
            return;

        switch (tool)
        {
            case WorkerTool.Pickaxe:
                SpawnHeavyToolSwing(facingDirection, toolSpriteSheetY: 80);
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
