# Verified item IDs

All entries confirmed against the base game DLL via `ilspycmd`. Use the constants in
`Dayswork/GameItemIds.cs` rather than inline string literals.

| Constant              | QualifiedItemId | Verified via                                        |
|-----------------------|-----------------|-----------------------------------------------------|
| `GameItemIds.Wood`    | `(O)388`        | Game DLL: `public const string woodID = "388"`      |
| `GameItemIds.Stone`   | `(O)390`        | Game DLL: `public const string stoneID = "390"`     |
| `GameItemIds.TreeFertilizer` | `(O)805` | Code comment in `CropCatalogProvider.cs` + DLL name match |

## Tapper detection

Do **not** use hardcoded IDs for tappers. The game provides `StardewValley.Object.IsTapper()`
which checks the `tapper_item` context tag. This covers both Tapper and Heavy Tapper (and any
modded tappers). See `ObjectTargetClassifier.HasTapper()`.

Confirmed via DLL decompile:
```
public virtual bool IsTapper() => HasContextTag("tapper_item");
```
