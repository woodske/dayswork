# Build and Test Summary — SVE Compatibility

Final Build & Test for the Stardew Valley Expanded compatibility change (units U-SVE-01..04). The base build/unit-test mechanics are unchanged from the redesign change — see [build-instructions.md](build-instructions.md) and [unit-test-instructions.md](unit-test-instructions.md). SVE-specific manual scenarios are in [sve-integration-test-instructions.md](sve-integration-test-instructions.md).

## Build status
- **Tool**: MSBuild / `dotnet` (.NET 6, SMAPI, xUnit + FsCheck).
- **Compile-only**: `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **Success — 0 warnings / 0 errors**.
- **Deployed**: `dotnet build Dayswork.sln` → success, deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Automated test status
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **378 passed / 1 expected skip / 0 failed**.
- Coverage added by the SVE change (xUnit + FsCheck, PBT full mode): expansion profile no-ops & selector, animal-building capacity policy, premium tier mapping, farm-map entrance overrides, exact-first + unique-name building resolution (incl. duplicate same-type buildings), category-based animal-product detection.

## Unit completion
| Unit | Scope | Status |
|---|---|---|
| **U-SVE-01** | Provider foundation + detection + vanilla invariance (C-19..C-23, M-22/M-23) | Complete |
| **U-SVE-02** | SVE farm maps + worker entrance (signature-keyed overrides; Grandpa's Farm (112,51), Frontier (142,16); blocked-tile fallback) | Complete |
| **U-SVE-03** | Premium animal buildings (data-driven capacity; Premium→Deluxe tier; auto-feed honored; auto-machines naturally skipped) | Complete |
| **U-SVE-04** | New content (category-based product pickup), content-override seam (no verified gap), multi-building unique keying | Complete (S-25 shed greenhouse deferred → TODO-10) |

## Test execution summary
### Unit / property tests
- **Total**: 379 (378 passed, 1 expected skip, 0 failed). **Status: Pass.**

### Integration (manual, in-game) — see sve-integration-test-instructions.md
- S1 Vanilla invariance · S2 SVE detection · S3 Farm-map entrances · S4 Premium buildings · S5 Multi/duplicate buildings · S6 Animal-product pickup · S7 Content classification + graceful skip · S8 Standard Grandpa's Farm greenhouse.
- **Status**: To be executed by the maintainer in vanilla + SVE environments. Playtest-confirmed so far this change: SVE farm-map entrances, premium tier pricing/auto-feed, multi/duplicate-building servicing with correct walking, animal-product pickup, and the Work Scope UI wrapping.

### Performance
- See [performance-test-instructions.md](performance-test-instructions.md). SVE additions are O(1) per-object/per-lookup on existing scan/resolve paths (NFR-SVE-02); detection runs once at GameLaunched and is cached. No new per-frame/per-tile cost. **Status: Pass (informal).**

### Security
- **N/A** — Security Baseline extension disabled; no network/PII/auth/external-input surface (NFR-SVE: security N/A).

## Deviations & caveats (this change)
- **DEV-SVE-01 — Content-override table empty (verified-gap policy).** SVE introduces no custom `ResourceClump` indices (only a `performToolAction` Harmony patch) and its custom wild trees use the generic `Tree` path, so the `SveExpansionProfile` content-override table is intentionally empty (pure passthrough). The seam exists for future verified gaps.
- **DEV-SVE-02 — Shed greenhouse deferred (TODO-10).** `Custom_GrandpasShedGreenhouse` is quest-gated and multi-hop (Farm → shed → greenhouse, farm-type-specific tile-action warps); reaching it needs multi-hop worker navigation, deferred. The standard Grandpa's Farm greenhouse is covered.
- **DEV-SVE-03 — Per-building animal work ordering (TODO-09).** Indoor-per-building then all-outdoor; functional but backtracks for spread-out buildings.
- Carry-over redesign caveats: see [redesign-deviations-and-caveats.md](redesign-deviations-and-caveats.md).

## Overall status
- **Build**: Success (0/0). **Automated tests**: Pass (378/1 skip/0). **Manual SVE integration**: to be run by the maintainer (key scenarios already playtest-confirmed). **Vanilla invariance**: preserved by design (null-object profile).
- **Ready for Operations**: Yes (Operations is a placeholder; this is a SMAPI mod — "operations" = build/deploy to the Mods folder, already automated).

## Next steps
All four SVE units are complete and verified. Remaining follow-ups are tracked as TODO-09 (animal work ordering) and TODO-10 (shed greenhouse / multi-hop navigation). Recommend the maintainer run the S1–S8 manual matrix in both vanilla and SVE environments before tagging a release.
