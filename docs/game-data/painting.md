# Painting: `BuildingPainter` and `BuildingPaintColor`

The game's own recolouring machinery. Dayswork uses it for two things: the worker's colour variants
(2.0 Phase 3, shipped) and — later — paintable offices (Phase 5).

## Verified sources

Confirmed on **2026-09-07** by decompiling `Stardew Valley.dll` v1.6.15 with `ilspycmd`:

```powershell
ilspycmd -t StardewValley.BuildingPainter "X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"
ilspycmd -t StardewValley.BuildingPaintColor "X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"
ilspycmd -t StardewValley.Utility "X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll" |
  Select-String "public static void RGBtoHSL|public static void HSLtoRGB" -Context 0,45
```

`Data/PaintData` and the absence of a vanilla "paint bucket" item are recorded in
[multiplayer-and-ownership.md](multiplayer-and-ownership.md) (correction 4 of the 2.0 plan).

## `BuildingPainter.Apply(Texture2D base, string maskPath, BuildingPaintColor color)`

Returns a **new** `Texture2D` (`Name = "@BuildingPainter.paintedTexture"`), or **`null`**. It is not
building-specific — it takes any texture, so it works on an NPC sprite sheet unchanged.

**Returns null in two cases**, both of which a caller must handle:

1. The mask asset failed to load (`Game1.content.Load<Texture2D>(maskPath)` threw). The failure is
   cached — `paintMaskLookup[maskPath] = null` — so *every subsequent call for that mask also
   returns null* for the rest of the session.
2. `color.RequiresRecolor()` is false, i.e. all three `Color*Default` flags are still `true`.

## The mask

- Read once per mask path into a `static Dictionary<string, List<List<int>>> paintMaskLookup`,
  keyed by the asset name and never invalidated.
- Pixels are matched by **exact colour equality** against `Color.Red` (255,0,0,255),
  `Color.Lime` (0,255,0,255) and `Color.Blue` (0,0,255,255) → paint regions 1, 2 and 3. Anything
  else (including transparent) is left alone. Off-by-one channel values match nothing, silently.
- Matching is by **flat pixel index** into the texture's pixel array, so **the mask must be exactly
  the same dimensions as the texture it paints** — a size mismatch mis-paints with no error.
  (`Dayswork.Tests/Integration/FarmhandPaintMaskTests.cs` guards that pairing.)

## The colour numbers are effectively absolute, not shifts

Per region, `Apply` runs `_ApplyPaint` **twice**:

```csharp
_ApplyPaint(0, -100, 0, pixels, region);                    // desaturate to grey
_ApplyPaint(colorNHue, colorNSaturation, colorNLightness, pixels, region);
```

`_ApplyPaint` converts each pixel with `Utility.RGBtoHSL` (standard HSL: hue in **degrees 0–360**,
saturation and lightness in **0–1**), then `h += hue; s += saturation/100; l += lightness/100`, wraps
hue and clamps s/l to [0,1], and converts back with `Utility.HSLtoRGB`. Because the first pass leaves
every pixel grey (`s = 0`, and `RGBtoHSL` reports `h = 0` for grey), the second pass reads as:

| Field | Range | Effect |
|---|---|---|
| `Color*Hue` | 0–359 | The resulting hue, in degrees. |
| `Color*Saturation` | 0–100 | The resulting saturation, as a percentage. |
| `Color*Lightness` | −100…100 | Percentage points **added to the pixel's own lightness** — this is what preserves the sprite's shading ramp. |

Alpha is never touched.

## `BuildingPaintColor`

A netcode object (`NetString ColorName`, and per region a `NetBool Color*Default` plus three
`NetInt`s). Construct it plainly and set `.Value` on the fields; a region whose `Color*Default` stays
`true` is skipped entirely and keeps the base texture's colours.

## How Dayswork uses it

`Dayswork/Worker/FarmhandAppearance.cs` serves `Characters/DaysworkFarmhand_<key>` as a `LoadFrom`
that paints the base sheet through `assets/farmhand_PaintMask.png` (cap = red, shirt = lime,
overalls = blue — see [farmhand-art.md](farmhand-art.md)). The palettes themselves are pure data in
`Dayswork.Core/Domain/WorkerAppearance.cs`. Because each variant is a real content asset, the
worker's appearance travels between players as nothing but a texture name
(`FarmhandNpc.getTextureName()` derives it from synced `modData`), with no custom sync.
