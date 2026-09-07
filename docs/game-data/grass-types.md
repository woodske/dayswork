# Grass types

`StardewValley.TerrainFeatures.Grass.grassType` is a `NetByte`. Verified constants from
`Stardew Valley.dll` decompile (ilspycmd):

| Constant     | Value |
|--------------|-------|
| springGrass  | 1     |
| caveGrass    | 2     |
| frostGrass   | 3     |
| lavaGrass    | 4     |
| caveGrass2   | 5     |
| cobweb       | 6     |
| blueGrass    | 7     |

Use `Grass.blueGrass` (the public const) to reference value 7 in code.

Blue grass (type 7) behaves like springGrass for growth/spreading; the game checks
`grassType.Value == 7` in several places alongside `== 1` (springGrass).
