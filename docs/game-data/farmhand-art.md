# Farmhand art reference

This is the canonical art/animation reference for replacing the temporary Marnie
farmhand sprite and portrait.

## Verified sources

Confirmed against the local Stardew Valley install at
`X:\Steam\steamapps\common\Stardew Valley` on 2026-06-18 by decompiling
`Stardew Valley.dll` v1.6.15.24356 with `ilspycmd`, and against the live
Dayswork worker code:

- `StardewValley.NPC.defaultSpriteWidth = 16`
- `StardewValley.NPC.defaultSpriteHeight = 32`
- `StardewValley.NPC.portrait_width = 64`
- `StardewValley.NPC.portrait_height = 64`
- `StardewValley.AnimatedSprite.GetSourceRect(...)` indexes sprite frames
  left-to-right across the texture width, then wraps to the next row.
- `StardewValley.FarmerSprite` defines many richer player body animation
  constants, while `StardewValley.FarmerRenderer` draws the farmer body, arms,
  tools, and effects as separate layered pieces.

The current Dayswork implementation uses `FarmhandNpc` with
`new AnimatedSprite(PlaceholderSpritePath, 0, 16, 32)`. `WorkerMovementDriver`
plays walking frames in four-frame directional rows, and `ToolSwapAnimator`
currently reuses the walk sheet's second/fourth frames as generic work poses.

## Current drop-in contract

The minimal replacement for Marnie is a standard NPC-style sheet:

- File: `Dayswork/assets/farmhand.png`
- Size: `64x128` pixels.
- Frame size: `16x32` pixels.
- Grid: 4 columns x 4 rows, transparent background.
- Draw scale: Stardew's normal character scale (`4f`), so each frame appears
  as `64x128` screen pixels.

Frame rows:

| Row | Frames | Facing | Idle frame |
|-----|--------|--------|------------|
| 0 | `0-3` | Down | `0` |
| 1 | `4-7` | Right | `4` |
| 2 | `8-11` | Up | `8` |
| 3 | `12-15` | Left | `12` |

Current action poses:

- Generic work pose: `row * 4 + 1`
- Alternate reach/collect pose: `row * 4 + 3`

`ToolSwapAnimator` uses the alternate pose for harvest crops, collect fruit,
and feed animals. Everything else currently uses the generic pose.

The portrait replacement should be:

- File: `Dayswork/assets/farmhand-portrait.png`
- Frame size: `64x64` pixels.
- Minimum: one neutral `64x64` frame.
- Recommended: `64x384` with six vertical frames matching the vanilla NPC
  portrait order: neutral, happy, sad, custom/concerned, blush, angry.

## Style requirements

- Match Stardew-compatible pixel art: crisp pixels, no antialiasing, no soft
  generated-image edges after cleanup.
- Use a transparent PNG with no frame padding.
- Keep feet and shadow alignment consistent across every frame; the visual
  baseline should sit on the bottom of each `16x32` cell.
- Preserve a readable silhouette at 4x scale. The hat/hair/head shape should be
  recognizable from front, side, and back.
- Use a restrained farmhand palette: work clothes, boots, gloves optional.
  Prefer a small set of ramped colors with top-left lighting and a 1-2 px
  colored outline.
- Avoid modern clothing details, photoreal shading, gradients, tiny unreadable
  accessories, or colors that disappear against grass/soil.

## Planned animation direction

Dayswork should separate body animation from tool/effect sprites, matching the
player character model instead of baking every tool into the farmhand body
sheet.

Reasons:

- Large tools extend outside a `16x32` NPC frame. Current axe/pick/watering-can
  overlays already draw above and beside the worker, far outside the body cell.
- Tool/effect sprites need direction-specific layer depth. For example, tools
  may need to appear behind the worker when facing up but in front for side or
  down-facing actions.
- Stardew's own player character separates this problem: `FarmerSprite` chooses
  body/arm animation frames, while `FarmerRenderer` layers tools and effects
  separately.
- Keeping tools separate lets Dayswork reuse one set of body frames with
  multiple tool levels, future custom tools, upgrade colors, and effects.
- Baked-in tools would require either oversized cells or custom draw offsets,
  then extra work for collision-adjacent alignment, emotes, stamina bar
  placement, and layer sorting.

The preferred implementation is:

1. Expand the farmhand body sheet with action-specific body/arm poses.
2. Add custom Dayswork tool/effect overlay sprites.
3. Synchronize both from `ToolSwapAnimator` so each work beat picks a body
   animation and a matching tool/effect overlay.

## Future expanded body sheet

The expanded sheet should keep `16x32` body cells unless there is a hard visual
reason to move to custom drawing. A practical target is four directions with
2-4 frames per action family.

Required action families:

| Family | Used for |
|--------|----------|
| Walk/idle | movement, waiting, stopped worker |
| Generic reach | deposits, shopping, no-tool interactions |
| Harvest/pluck | crop harvest, fruit tree shake/collect |
| Feed/place | hay hopper, trough placement |
| Pet | animal petting |
| Plant/fertilize | managed-crop seed and fertilizer placement |
| Hoe | tilling managed-crop tiles |
| Watering can | crop watering |
| Scythe | weeds and grass |
| Pickaxe | rocks, ore, boulders, meteorites |
| Axe | trees, stumps, logs, twigs |
| Milk pail | cow/goat collection |
| Shears | sheep collection |

If each family has four frames per direction, the sheet would contain:

- Base walk/idle: 16 frames.
- 12 action families x 4 directions x 4 frames = 192 frames.
- Total: 208 frames.
- At 4 columns, 16x32 cells: `64x1664` pixels.

This is a planning size, not a runtime requirement. The first upgrade can be
smaller: add only the body frames needed to replace the most visible generic
poses, while leaving existing tool overlays in place.

## Image-generation workflow notes

Use the image generation result as a draft, then normalize it into exact game
pixels:

1. Generate a character sheet concept on a flat chroma-key background or
   transparent-safe background.
2. Remove the background and inspect the alpha channel.
3. Downsample/crop manually or with nearest-neighbor tooling into exact
   `16x32` cells.
4. Pixel-clean silhouettes, hands, boots, and face readability by hand.
5. Verify each frame at native size and at Stardew's 4x scale before wiring it
   into the mod.

Do not reference generated art directly from outside the repo. Final project
assets belong under `Dayswork/assets/` and are copied by the existing project
file.
