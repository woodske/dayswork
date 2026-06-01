# Requirements - TODO-10 SVE Grandpa's Shed Greenhouse

**Change**: Implement TODO-10, enabling Dayswork to service SVE's quest-unlocked Grandpa's Shed greenhouse through source-grounded multi-hop navigation.
**Status**: Requirements Analysis generated from answered [todo-10-requirement-verification-questions.md](todo-10-requirement-verification-questions.md) on 2026-05-31.
**Source inputs**: TODO-10 entry in `aidlc-state.md`, U-SVE-04 deferral notes, current Dayswork navigation/scope code, and SVE source under `C:\Users\kwood\Repos\StardewValleyExpanded`.

## 1. Intent Analysis

| Field | Value |
|---|---|
| User request | "using ai-dlc, do task TODO-10" |
| Request type | Enhancement / SVE compatibility follow-up |
| Scope estimate | Multiple components: SVE profile data, greenhouse scope selection, cross-location worker navigation, chest/deposit discovery, shift orchestration, tests, and manual SVE playtest instructions |
| Complexity estimate | Complex |
| Requirements depth | Comprehensive |
| Primary risk | Multi-hop route correctness without vanilla behavior change or item loss |

## 2. Grounded Source Context

- The target location is `Custom_GrandpasShedGreenhouse`, not the standard Grandpa's Farm greenhouse named `Greenhouse`; the standard greenhouse path is already covered by existing Dayswork greenhouse support.
- SVE loads `Custom_GrandpasShedRuins`, `Custom_GrandpasShed`, and `Maps/Custom_GrandpasShedGreenhouse` from Grandpa's Shed map assets in `content.json`.
- SVE's farm computer patch treats `Custom_GrandpasShedGreenhouse` as an extra greenhouse location (`HarmonyPatch_FarmComputerLocations.cs`).
- SVE route data is farm-map dependent: `GrandpasShed.json` patches `Warps_GrandpasShed_IF2R`, `Warps_GrandpasShed_GF`, and `Warps_GrandpasShed_NF`; IF2R's map action reaches `Custom_GrandpasShedRuins`, while Frontier's fixed shed map reaches `Custom_GrandpasShed`.
- SVE quest/event state changes the shed complex: event data references `ShedRepaired`, event `2554906`, and refurbished event `2554907`. TODO-10 does not require Dayswork to inspect those flags directly; it validates live route availability instead.
- `Custom_GrandpasShedOutside` has a `DefaultArrivalTile` and artifact-spot data in `LocationsData.json`, but Q1/Q6 keep outdoor/ruins work out of scope for this task.

## 3. Functional Requirements

### 3.1 Scope and selection

- **FR-T10-01 - Target work area.** TODO-10 services `Custom_GrandpasShedGreenhouse` as an indoor crop-work location. The main `Custom_GrandpasShed` interior is included only for chest/deposit support, not for worker tasks. `Custom_GrandpasShedOutside` and `Custom_GrandpasShedRuins` are not serviced as work areas. *(Q1=B, Q6=A)*
- **FR-T10-02 - Single greenhouse selection model.** Keep the current single `GreenhouseSelection(LocationName)` scope model. When SVE makes the shed greenhouse discoverable/available, expose it as a selectable alternative greenhouse location rather than allowing multiple greenhouse locations in one contract. *(Q2=A)*
- **FR-T10-03 - No automatic inclusion.** Selecting the standard greenhouse must not automatically include the shed greenhouse. The player chooses the greenhouse-like location intentionally. *(Q2=A, out-of-scope C)*

### 3.2 Supported SVE route coverage

- **FR-T10-04 - Covered farm maps.** Support all SVE farm maps already in the SVE compatibility scope: Immersive Farm 2 Remastered, Grandpa's Farm, and Frontier Farm. Route data for each must be grounded in SVE source and validated at runtime. *(Q3=A)*
- **FR-T10-05 - Explicit SVE route provider.** Add explicit route data for SVE multi-hop navigation. Each route consists of source-grounded hops with target locations and approach/arrival tiles. The worker walks to each hop's approach tile, then uses the existing warp/location-transition model to move between locations. *(Q4=A)*
- **FR-T10-06 - No generic cross-expansion graph search.** This change must not build a broad runtime graph over all Content Patcher warps or tile actions. The route provider can be designed for future extension, but TODO-10 implements only the known SVE shed greenhouse routes. *(Q4=A, Q3=A)*
- **FR-T10-07 - No direct greenhouse shortcut.** The worker must not directly warp from the farm to the shed greenhouse as the primary success path. Direct warp fallback is out of scope. *(Q4=A, Q8=A)*

### 3.3 Availability and failure behavior

- **FR-T10-08 - Runtime route validation.** The worker attempts shed-greenhouse work only when the live locations and every configured hop validate at runtime. Validation includes target location existence, reachable approach/exit tiles, and passable worker standing tiles where applicable. *(Q5=A)*
- **FR-T10-09 - No direct SVE quest-flag dependency.** Dayswork should not inspect SVE quest, event, or mail flags as the authority for scheduling. The live route and location state are authoritative. *(Q5=A)*
- **FR-T10-10 - Graceful route failure.** If the route is unavailable, blocked, or cannot be resolved during a shift, skip the shed-greenhouse batch, continue the rest of the shift, preserve item safety, and log a maintainer-facing reason. No player-facing mail or needs-attention state is introduced for this skip. *(Q8=A)*

### 3.4 Work execution

- **FR-T10-11 - Greenhouse tasks only.** Shed-greenhouse work runs only existing greenhouse crop services: Water Crops and Harvest Crops. It uses existing greenhouse pricing, stamina, batching, task execution, and output provenance semantics. *(Q6=A)*
- **FR-T10-12 - Existing greenhouse scan behavior.** Shed-greenhouse crop scanning must reuse the existing indoor/greenhouse scan path where possible, with only route/location resolution changes needed for the multi-hop location.
- **FR-T10-13 - Vanilla greenhouse unchanged.** Existing vanilla and standard SVE greenhouse behavior must remain unchanged when `Custom_GrandpasShedGreenhouse` is not selected. *(SVE compatibility invariant)*

### 3.5 Output destinations and item safety

- **FR-T10-14 - Destination support.** Preserve the existing shipping/bin and farm-chest output behavior. If chests are discovered inside the selected shed greenhouse, they may be used as deposit destinations. Per Q1, chests in the main `Custom_GrandpasShed` interior may also be offered for shed-greenhouse output, but only as deposit destinations. *(Q1=B, Q7=A)*
- **FR-T10-15 - Deposit routing.** Deposit trips to shed greenhouse or main shed chests must use the same source-grounded route provider and validation model as work entry/exit, rather than assuming a single farm door.
- **FR-T10-16 - No item loss.** All existing buffer, overflow, and undelivered-item safeguards continue to apply. A failed route or deposit trip must never discard collected items. *(Q8=A, SVE NFR carry-forward)*

### 3.6 Compatibility and isolation

- **FR-T10-17 - Vanilla invariance.** With SVE absent, with SVE present but no shed greenhouse selected, or with the route unavailable, existing behavior must remain unchanged except for maintainer diagnostics when an explicitly selected shed route cannot validate.
- **FR-T10-18 - Centralized SVE identifiers.** SVE-specific location names, route identifiers, and farm-map route data must live behind the existing expansion-compatibility seam or a closely related route provider, not scattered across general orchestration code.

## 4. Non-Functional Requirements

- **NFR-T10-01 - Reliability.** Route validation and route failure must be total and non-throwing. Unsupported or unavailable SVE state results in a skipped shed-greenhouse batch with logging.
- **NFR-T10-02 - Performance.** Route lookup must be bounded and data-driven. No per-frame route graph construction, reflection over Content Patcher data, or repeated full-map route discovery in hot paths.
- **NFR-T10-03 - Testability.** Route-selection and route-validation logic should be expressed through pure model types where possible, so source-route invariants can be tested without SMAPI.
- **NFR-T10-04 - Manual verification.** Because the final route depends on live SVE maps and Content Patcher patches, the change requires at least one manual SMAPI playtest completing shed-greenhouse work under SVE. *(Q9=A)*
- **NFR-T10-05 - Maintainability.** The implementation should remain narrow: explicit SVE route data now, shaped so future expansion route providers can be added without disturbing vanilla paths.
- **NFR-T10-06 - Security.** Security Baseline is disabled for this local SMAPI mod change. No network, authentication, secrets, or PII surface is introduced. *(Q10=B)*
- **NFR-T10-07 - PBT mode.** Property-Based Testing is enabled in partial mode for this change: PBT-02, PBT-03, PBT-07, PBT-08, and PBT-09 are blocking where applicable. Route-model invariants from Q9 are still required for pure route logic. *(Q11=B, Q9=A)*

## 5. Test and Verification Requirements

- **TV-T10-01 - Pure examples.** Add example-based tests for known SVE route definitions and failure cases.
- **TV-T10-02 - FsCheck route properties.** Add FsCheck properties for pure route-model invariants, such as deterministic route selection, no duplicate hop execution, route validation totality, and route failure preserving skip/continue decisions.
- **TV-T10-03 - Integration tests.** Update relevant integration tests around greenhouse scope selection, chest discovery/deposit destination routing, and vanilla/SVE no-op behavior where the live SMAPI objects can be modeled.
- **TV-T10-04 - Manual SMAPI playtest.** Document and execute at least one in-game SVE playtest where a worker reaches `Custom_GrandpasShedGreenhouse`, performs greenhouse crop work, returns/deposits safely, and exits/wraps up without item loss.
- **TV-T10-05 - Regression verification.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`; deployed build verification is required before final approval if the maintainer is playtesting in-game.

## 6. Out of Scope

- Multiple greenhouse selections in one contract.
- Automatically adding the shed greenhouse when the standard greenhouse is selected.
- Generic cross-location graph discovery for all Content Patcher maps or non-SVE expansions.
- Servicing `Custom_GrandpasShedOutside`, `Custom_GrandpasShedRuins`, or the main shed interior as work locations.
- Clearing tasks inside the shed greenhouse.
- Direct-warp fallback to bypass blocked routes.
- Player-facing unavailable-route mail or needs-attention contract state.
- GrampletonFields or non-SVE expansion route support.

## 7. Extension Configuration (this change)

| Extension | Decision | Mode | Source |
|---|---|---|---|
| Security Baseline | Disabled | N/A | Q10=B |
| Property-Based Testing | Enabled | Partial - PBT-02, PBT-03, PBT-07, PBT-08, PBT-09 blocking where applicable | Q11=B |

## 8. Requirements Summary

TODO-10 implements the shed greenhouse as a selected alternative greenhouse location, with crop work only, explicit source-grounded multi-hop SVE routes for IF2R, Grandpa's Farm, and Frontier Farm, runtime route validation instead of SVE quest-flag coupling, graceful skip/continue behavior when unavailable, and item-safe chest/deposit support for the shed greenhouse plus main shed deposit-only chests.
