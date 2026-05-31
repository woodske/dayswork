# U-SVE-04 — New Content + Grandpa's Shed — Code Generation Plan

**Unit**: U-SVE-04 (final SVE unit) · **Stories**: S-24, S-25 · **Decisions**: Q1=A..Q5=A · **Patterns**: P-SVE4-01..05 · **Folded-in**: TODO-07, TODO-08

Ordered lowest-risk first; each step builds + tests green before the next. No new components, no save-schema change. Vanilla stays identical via the null-object profile.

## Steps

- [x] **Step 1 — Category-based animal-product detection (TODO-07 / S-24).** DONE — category {-5,-18} + legacy parity include; pure overload + `AnimalProductDetectionTests`; closes TODO-07.
- [x] **Step 2 — Content overrides.** DONE (no-gap): source audit found no custom clump indices (SVE only patches `ResourceClump.performToolAction`) and custom wild trees use the generic `Tree` path → override table stays empty (passthrough); no classifier change.
- [ ] **(superseded — original Step 1 text below)**
  Replace `WorkAreaScanner.AnimalProductObjectIds` whitelist + `IsAnimalProductForageObject` with a category test over the object's `Category` ∈ { Egg `-5`, Milk `-6` (if ground), Animal Goods `-18`, Truffle/forage }. **Verify Truffle's exact category from vanilla object data first** and assemble the set so it reproduces the legacy whitelist (parity), then add the SVE products (Goose Egg -5, Camel Wool -18 flow in automatically). Keep a tiny explicit exclude set (default empty). `ShiftOrchestrator.InvokeCollectAnimalProduct` keeps using the shared predicate. Guard the category read so it never throws.

- [ ] **Step 2 — Content-classification override wiring (S-24 verified gaps).**
  In `ObjectTargetClassifier.ClassifyAxe`/`ClassifyPick`, build a `ContentDescriptor` for the live clump/tree/object and consult `ExpansionCompatService.TryClassifyContentOverride(...)` **before** the vanilla classify/skip; map `Axe`/`Pick` results, fall through on `None`. **Audit SVE source for custom resource-clump sheet indices / special trees**; populate `SveExpansionProfile`'s content-override table for verified gaps, else leave it empty (passthrough) with a note. Requires threading the compat seam to the classifier call site (it's static today — pass via the existing `ModEntry.ExpansionCompat` accessor, consistent with the other consumers).

- [ ] **Step 3 — Grandpa's Shed work location (S-25 / Q6=A).**
  Add the plantable Grandpa's Shed greenhouse id (`Custom_GrandpasShedGreenhouse`; confirm exact `NameOrUniqueName` at runtime) to `SveExpansionProfile`'s expansion work-location set. Ensure `BuildingWorkNavigator`/`IndoorWorkScanner`/`BuildingLocationResolver`/`ChestResolver` include `IsExpansionWorkLocation` locations so the worker enters (via warp/arrival tile), scans crop work (Water/Harvest), and can deposit there. Confirm reachability/entry at code-gen; default to crop work only.

- [x] **Step 3 — Grandpa's Shed work location (S-25). DEFERRED (TODO-10).** Source investigation (chosen "investigate-lighter-path") found the shed greenhouse is quest-gated and multi-hop (Farm → shed → `Custom_GrandpasShedGreenhouse`) via farm-type-specific tile-action warps not present in `farm.warps`; no single-warp path exists, so it needs multi-hop navigation the worker lacks. Deferred to TODO-10; the standard Grandpa's Farm greenhouse (`"Greenhouse"`) is already covered by existing greenhouse support. No code added.
- [x] **Step 4 — Unique-name building keying (TODO-08). DONE + playtest-confirmed.** Keyed selections on `NameOrUniqueName` end-to-end (`ChestResolver.GetBuildingOutlines`, `BuildingLocationResolver` exact-match + `NormalizeLocationName`, `LegacyScopeBootstrapper.TryClassify` tier-only inference with unique key, `AnimalTaskHandler` home-matching with type-name legacy fallback, GUID stripped for UI). Fixed two follow-on bugs found in playtest: base-building keying (vanilla classify path) and the door-resolution regression (`TryResolveWith` used a requestedName interior fallback that lent the first building's door to all). Temporary diagnostics added then stripped after confirmation. Tests extended (`BuildingLocationResolverTests`).
- [ ] **(superseded original Step 4 below)**
  Switch `ChestResolver.GetBuildingOutlines` to key `BuildingOutline.LocationName` on `indoors.NameOrUniqueName` (unique per instance) instead of `indoors.Name`. **Consistency requirement**: every site that matches a selection's `LocationName` must use the same identifier — `BuildingLocationResolver` (add `NameOrUniqueName` to the exact-match set), the runtime `selectedAnimalHomes` membership in `ShiftOrchestrator`, and `AnimalTaskHandler.ResolveHomeLocation` (animal → home interior). Keep the U-SVE-03 exact-then-loose fallback so legacy saved contracts (type-name keys) still resolve (no schema change). If runtime shows same-type interiors actually share a `NameOrUniqueName`, fall back to a tile-coord discriminator (documented, not assumed).

- [ ] **Step 5 — Tests.**
  `Dayswork.Tests/Compat` + scanner/resolver: category-detection **parity** (every legacy whitelist id still detected) + SVE products (Goose Egg/Camel Wool) detected + FsCheck totality; content-override passthrough/determinism; `IsExpansionWorkLocation` membership (Grandpa's Shed true, vanilla false); unique-key resolution for two same-type buildings (extend `BuildingLocationResolverTests`); vanilla invariance.

- [ ] **Step 6 — Verify.**
  `dotnet build /p:EnableModDeploy=false` → 0/0; `dotnet test` → all green. Then deploy-enabled build if the game is closed.

- [ ] **Step 7 — Document + state.**
  Write `construction/u-sve-04-content-grandpas-shed/code/code-summary.md` (incl. playtest checklist: goose egg/camel wool/wool pickup; Grandpa's Shed serviced; two same-type buildings both serviced); tick checkboxes; update `aidlc-state.md`, `audit.md`; close TODO-07/TODO-08.

## Vanilla invariance guard
Null-object profile → empty override table + empty work-location set; category detection reproduces the legacy whitelist (Step 5 parity); unique keying falls back to existing resolution for legacy/vanilla. No vanilla behavior change.

## Risk notes
- **TODO-08 (Step 4)** is the riskiest — cross-site key consistency. Validated by playtest (two same-type buildings). If runtime evidence contradicts the `NameOrUniqueName` assumption, switch to a coord-based discriminator.
- **Truffle category (Step 1)** and **custom clumps (Step 2)** and **Grandpa's Shed id/entry (Step 3)** are confirmed from source/runtime at implementation time — not assumed.
