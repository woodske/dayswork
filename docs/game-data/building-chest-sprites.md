# Building chest sprites

Verified against the local Stardew Valley install at
`X:\Steam\steamapps\common\Stardew Valley` on 2026-06-13 by decompiling
`Stardew Valley.dll` with `ilspycmd`.

Normal player chests are `StardewValley.Objects.Chest` instances with item id
`130`, i.e. qualified id `(BC)130`. The big-craftable source rectangle helper
`StardewValley.Object.getSourceRectForBigCraftable(...)` returns a 16x32 source
rectangle. `Chest.draw(...)` draws player chests at `(tileX * 64, (tileY - 1) *
64)` with scale `4f`, so a placed chest occupies one tile wide and two tiles
tall on screen.

For the Dayswork office sprite (`80x106`, 5x3 tile footprint), the footprint is
bottom-aligned and starts at source `y=58`. The built-in office chest display
tiles are:

- Input chest: local tile `(1,2)` -> transparent sprite rect `x=16, y=74, w=16, h=32`
- Output chest: local tile `(3,2)` -> transparent sprite rect `x=48, y=74, w=16, h=32`

The hand-drawing outline at `Dayswork/assets/hq-building-outline.png` is a 3x
template in a 240x336 canvas. The 80x106 source-space cabin is scaled to
240x318 and bottom-aligned, leaving 18 pixels of transparent headroom at the
top. Its scaled chest cutouts are:

- Input chest: `x=48, y=240, w=48, h=96`
- Output chest: `x=144, y=240, w=48, h=96`

The temporary runtime alignment asset `Dayswork/assets/hq-building-temp.png`
uses the normal 160x122 sheet layout and keeps the verified source-space
cutouts transparent in both the base and glow regions.
