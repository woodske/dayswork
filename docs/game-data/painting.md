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

## `Data/PaintData` — the vanilla table, and the region names it reuses

Dumped on **2026-09-07** from `Content/Data/PaintData.xnb` and `Content/Strings/Buildings.xnb`
(both LZX-compressed, so `strings` shows nothing — see
[game-content-search.md](game-content-search.md) for the ContentManager dumper used):

| Key | Value |
|---|---|
| `House` | `Building/-50 -10/Roof/-25 0/Trim/-25 -8` |
| `Log Cabin` | `Building/-20 20/Roof/-15 5/Trim/-10 5` |
| `Stone Cabin` | `Building/-15 25/Roof/-15 5/Trim/-5 5` |
| `Plank Cabin` | `Building/-55 -20/Roof/-10 5/Trim/-55 -30` |
| `Beach Cabin` | `Building/-10 0/Roof/0 5/Trim/-5 5` |
| `Neighbor Cabin` | `Building/-10 0/Roof/-15 10/Trim/-10 5` |
| `Rustic Cabin` | `Building/-20 5/Roof/-5 5/Trim/-5 0` |
| `Trailer Cabin` | `Building/-5 0/Roof/-5 5/Trim/-5 0` |
| `Stable` | `Building/-20 5/Roof/-25 0/Trim/-15 0` |
| `Big Shed` | `Building/-45 -10/Roof/-20 5/Trim/-25 0` |
| `Deluxe Coop` | `Building/-25 0/Roof/-15 5/Trim/-25 0` |
| `Deluxe Barn` | `Building/-15 0/Roof/-10 5/Trim/-10 5` |

Two things follow, and both are load-bearing for a modded building:

- **Every vanilla entry uses the same three region names: `Building`, `Roof`, `Trim`** — and
  `Strings/Buildings` already ships `Paint_Region_Building`, `Paint_Region_Roof` and
  `Paint_Region_Trim`, translated in all 13 shipped languages. A custom building that reuses those
  names gets translated labels for free and **needs no `Strings/Buildings` edit at all**; inventing a
  name (`Walls`) would mean shipping and translating a string, and overwriting one of vanilla's own
  keys to do it. `LoadRegionData` falls back to the raw region name when the string is absent, so the
  failure mode is an untranslated label, not an error.
- The two numbers are the **lightness slider's min and max** for that region (`ColorSliderPanel`),
  not a default colour — they are what keeps a paint job from crushing that region's shading ramp to
  black or white. They are tuned per building, and `-100 100` (the parser's fallback) is not what any
  vanilla building actually uses.

## The office's paint mask

`assets/farmhand_office_PaintMask.png` (160×122, matching `assets/farmhand_office.png`) is served as
`Mods/Bindicle.Dayswork/Building_PaintMask` — a name that is not ours to pick: `resetTexture()` asks
for `textureName() + "_PaintMask"`, and `textureName()` is the `BuildingData.Texture` we declare.
`Data/PaintData` gains `Bindicle.Dayswork_Office: "Building/-20 20/Roof/-15 10/Trim/-15 10"` (the Log
Cabin's ranges — the building this sprite is modelled on) in `HiringBuilding.OnAssetRequested`.

The mask was derived from the sprite rather than drawn by hand, which is worth knowing if it is ever
regenerated:

- **Roof** (`Color.Lime`) is a flood fill of the red shingle field from four seeds. A fill rather
  than a hue test, because a handful of the corner posts' shadow seams are red-hue too and a plain
  test sweeps them in.
- **Trim** (`Color.Blue`) is four authored boxes on the sheet's base sprite — left post `x 3–10`,
  right post `x 69–76` (both `y 50–98`), window frame `x 20–58, y 49–66`, door casing and slab
  `x 31–48, y 68–96` — intersected with the wood test below. Roof wins where they overlap, which is
  how the posts stay unpainted under the eaves.
- **Building** (`Color.Red`) is every remaining pixel that is *wood*: opaque, saturation ≥ 0.18, hue
  outside 60°–300°, above `y 99`. That test is what leaves the window glass, the stone plinth, the
  porch barrels' metalwork and the two wall plaques' contents alone.
- The glow overlay (`x ≥ 80`) and the smoke strip (`y ≥ 106`) are **unmasked**: warm light should not
  shift hue with the walls. `HiringBuildingOverlayRenderer` still draws them from
  `building.texture.Value` — the building's own, possibly painted, texture — rather than reloading
  the raw sheet, so a repainted office and its lit window can never disagree.

`Dayswork.Tests/Integration/OfficePaintMaskTests.cs` guards the size pairing, the three exact colours
and the `Data/PaintData` shape; `PaintMaskAsset` there is a ~100-line PNG reader so neither mask test
needs an image library.
