# Application Design Plan — Stardew Valley Expanded (SVE) Compatibility

**Stage**: Application Design (Inception). This plan defines *how* the SVE-compatibility components/services are designed. Artifact generation runs only after you approve answers.

**How to use this file**: answer each `[Answer]:` tag with a letter (or `X` + description). Reply "done" when finished. I'll analyze for ambiguities, ask follow-ups only if needed, then generate the design artifacts for your review.

**Inputs**:
- Requirements [sve-compatibility-requirements.md](../requirements/sve-compatibility-requirements.md); Stories S-21..S-26; Execution plan [sve-compatibility-execution-plan.md](sve-compatibility-execution-plan.md).
- Existing architecture: [components.md](../application-design/components.md) (C-## Core / M-## Mod), [services.md](../application-design/services.md) (S-A..S-G), strict `Dayswork.Core` (pure) / `Dayswork` (SMAPI) split.

---

## Grounded facts feeding this design (verified in source)

- **Detection idiom**: `helper.ModRegistry.GetApi(...)` is already used in `ModEntry`/`GMCMRegistrar`; `ModRegistry.IsLoaded(id)` is the natural check.
- **SVE is several mods**, not one — confirmed UniqueIDs:
  - Core content (premium buildings, crops, Grandpa's Shed): **`FlashShifter.StardewValleyExpandedCP`**; core C#: **`FlashShifter.SVECode`**.
  - Farm maps (separate, optional): IF2R **`flashshifter.immersivefarm2remastered`**, Grandpa's Farm **`flashshifter.GrandpasFarm`**, Frontier Farm **`flashshifter.FrontierFarm`**.
  - So the seam must expose both "SVE content present" and "which farm package is active."
- **Scope model** (verified): `AnimalBuildingScope`, `AnimalBuildingSelection`, and `AnimalBuildingPriceKey` all key on the enum `AnimalBuildingTier` (six vanilla values only).
- **Premium buildings** (verified): `AnimalHouse`, `MaxOccupants` 16, hopper `(BC)99`.

---

## Proposed component additions (for your review; Q1–Q5 refine these)

**New Core components (`Dayswork.Core/Compat/`, pure & testable):**
- **C-19 IExpansionProfile** — value/interface describing an expansion's compat data + pure lookups: per-map entrance overrides, content-classification overrides, work-location membership, premium-building mapping.
- **C-20 ExpansionProfileSelector** — pure selection of the active profile from a set of installed mod IDs (Vanilla default vs SVE).
- **C-21 VanillaExpansionProfile** — default profile with no overrides (everything falls through to existing vanilla behavior → guarantees NFR-SVE-01).
- **C-22 SveExpansionProfile** — SVE's concrete profile data; centralizes all SVE identifiers (NFR-SVE-07).
- **C-23 AnimalBuildingCapacityPolicy** — pure capacity derivation from a building's real occupant/trough data.

**New Mod components (`Dayswork/Compat/`):**
- **M-22 ExpansionDetector** — queries `ModRegistry` for known expansion IDs, builds the installed-ID set, logs the active profile once.
- **M-23 ExpansionCompatService** — the runtime seam: holds the active profile and applies it to live `Farm`/`AnimalHouse`/`GameLocation` objects (`ResolveFarmEntrance`, `ResolveAnimalBuildingCapacity`, `TryClassifyOverride`, `IsExpansionWorkLocation`, …). Vanilla-identical when the profile is the default. The single thing existing call sites depend on.

**Existing components that consume the seam** (call-site delegation; no SVE branches inline): `ModEntry` (wiring), `ShiftOrchestrator` (entrance), `AnimalTaskHandler` (capacity), `ObjectTargetClassifier` (classification hook), building navigators (`BuildingWorkNavigator`/`IndoorWorkScanner` for Grandpa's Shed).

---

## Design Questions

## Question 1 — Provider seam shape
A) **(Recommended)** Pure **ExpansionProfile** (data + pure lookups) + pure **ExpansionProfileSelector** in `Dayswork.Core`, wrapped by a thin Mod-side **ExpansionCompatService** that applies the profile to live game objects. Matches the Core-pure / Mod-adapter split; maximizes testable surface (S-26 PBT).
B) A single fat `IExpansionProvider` interface implemented imperatively by `VanillaProvider` / `SveProvider` (more OO; less pure data; harder to unit-test the parts touching game objects).
C) Per-concern interfaces (`IEntranceProvider`, `IAnimalBuildingProvider`, `IContentClassifier`, `IWorkLocationProvider`), each with vanilla + SVE implementations.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Where SVE-specific data lives
A) **(Recommended)** In `Dayswork.Core` as pure data (mod IDs, supported farm-map ids → entrance overrides, premium building keys, content overrides, Grandpa's Shed location id), centralized in the SVE profile type (NFR-SVE-07) and unit/PBT-testable without SMAPI.
B) In the `Dayswork` mod layer (closer to the SMAPI APIs that consume it), accepting those values aren't covered by the Core-only test project.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Animal-building capacity derivation scope
A) **(Recommended)** A **general** pure `AnimalBuildingCapacityPolicy` used by **both** vanilla and SVE — derive capacity from the building's actual occupant/trough data instead of the hardcoded 4/8/12 ladder. Fixes premium buildings and is a correctness improvement for vanilla too.
B) **SVE-only** capacity override; leave the vanilla hardcoded ladder untouched.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4 — Representing SVE premium tiers in the scope/pricing model
Today `AnimalBuildingScope`/`Selection`/`PriceKey` key on the 6-value `AnimalBuildingTier` enum.
A) **(Recommended)** The SVE provider **maps premium buildings to their nearest vanilla tier for scope/pricing** (Premium Coop → DeluxeCoop, Premium Barn → DeluxeBarn) while feeding capacity is data-driven (Q3). Keeps `Dayswork.Core` free of SVE-specific tiers; no new price keys or save-schema change.
B) **Extend `AnimalBuildingTier`** with `PremiumCoop`/`PremiumBarn` (plus new price-key entries + config) so premium is a first-class priced tier. More faithful pricing, but adds SVE-flavored values to the Core enum and touches pricing/config/persistence.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5 — How existing call sites consume the seam
A) **(Recommended)** Inject `ExpansionCompatService` via **constructor** into the `ModEntry`-built components that need it (orchestrator, animal handler, classifier hook, building navigators), consistent with the project's existing composition-root wiring.
B) Expose it as a **static/ambient accessor** (simpler call sites, but harder to test and against the current DI style).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (artifact generation — runs after answers approved)

- [x] Update `application-design/components.md` with C-19..C-23 and M-22..M-23 (SVE compat group) per Q1/Q2/Q3.
- [x] Update `application-design/component-methods.md` with the new components' method signatures (high-level; business rules deferred to Functional Design).
- [x] Update `application-design/services.md` with the expansion-detection/selection wiring in the ModEntry composition root and the runtime seam usage.
- [x] Update `application-design/component-dependency.md` with the new dependencies + the call-site delegation (vanilla path unaffected).
- [x] Reflect the premium-tier model decision (Q4) and capacity policy (Q3) in the relevant docs.
- [x] Consolidate into `application-design/application-design.md` (or a change-scoped SVE addendum) and update `aidlc-state.md` + `audit.md`.

## Mandatory artifacts (Step 3)
- [x] components.md, component-methods.md, services.md, component-dependency.md updated/extended for the SVE compat seam, validated for consistency.
