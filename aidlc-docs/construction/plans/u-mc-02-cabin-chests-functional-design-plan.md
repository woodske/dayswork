# Functional Design Plan - U-MC-02 Cabin Chests

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Functional Design
**Status**: Review required

## Plan Checklist

- [x] Load Functional Design rule details.
- [x] Load U-MC-02 unit definition and dependency context.
- [x] Load assigned stories S-31 and S-34.
- [x] Inspect existing `HiringBuilding`, `HiringBuildingInteraction`, and `ChestResolver` seams.
- [x] Load enabled Property-Based Testing extension rules.
- [x] Create functional design questions using required `[Answer]:` format.
- [x] Collect complete answers for all questions.
- [x] Analyze answers for ambiguity or contradictions.
- [x] Resolve clarification question for output chest selectability.
- [x] Generate functional design artifacts.
- [x] Present Functional Design completion gate.

## Answer Analysis

Answers received: Q1=A, Q2=A, Q3=A, Q4=B, Q5=B.

No answers are missing or syntactically invalid. Q4 conflicts with the approved U-MC-02 requirement text that says `ChestResolver` excludes both built-in chests from selectable destination lists. The user also clarified that the farmhand cabin input chest is where crop management draws supplies from, and the output chest is where task output can be deposited. That clarification is compatible with either an implicit output-chest fallback or an explicit selectable output-chest destination, so a follow-up clarification is required before generating Functional Design artifacts.

Clarification answer received: `B, output chest should remain default and selectable`.

Resolution: U-MC-02 intentionally overrides the earlier FR-MC-35 wording for the output chest only. The input chest remains excluded from selectable destination lists because it is the managed-crop supply reservoir. The output chest remains the default/fallback task-output deposit destination and is also selectable explicitly as a task-output/per-zone destination.

## Unit Context

U-MC-02 adds the dedicated Manage Crops input chest to the farmhand office and keeps both built-in office chests out of player-selectable per-zone destinations.

## In Scope

- Declare a second built-in `BuildingChest` on `HiringBuilding.BuildData()`.
- Add an input chest identity alongside existing output chest identity.
- Define idempotent input-chest backfill for pre-existing offices.
- Define programmatic i18n-backed built-in chest names.
- Define `ChestResolver` exclusion rules for both built-in office chests.
- Identify PBT-relevant idempotence/invariant properties for backfill and exclusion logic.

## Out of Scope

- Reading supplies from the input chest during shifts.
- Returning shopping leftovers to the input chest.
- Per-zone harvest output routing.
- Manage Crops authoring UI.
- Town shopping.

## Functional Design Questions

Please answer each question by filling in the letter after the `[Answer]:` tag. If none of the options match, choose the last option and describe the preference.

## Question 1
Where should the new input chest display tile be placed on the farmhand office footprint?

A) Use the symmetric porch tile left of the door: input at `(1, 2)`, existing output remains `(3, 2)`.
B) Place input beside output on the right side: input at `(4, 2)`, output remains `(3, 2)`.
C) Move both built-in chests to a new paired layout and update output placement too.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: A

## Question 2
When should the input-chest backfill run for pre-existing farmhand offices?

A) Run on every save load/day start as an idempotent ensure operation.
B) Run only once per save using a modData migration marker.
C) Run only when the farmhand office building is interacted with.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: A

## Question 3
How should programmatic names behave for the built-in office chests?

A) Always apply fixed i18n-backed names to both built-in chests, overriding generic/default chest names.
B) Apply fixed i18n-backed names only when the current chest name is blank or `"Chest"`.
C) Do not set chest names; rely only on `ChestResolver.GetDisplayName` fallback labels.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: A

## Question 4
How should `ChestResolver` treat the two built-in office chests for selectable destination lists?

A) Exclude both input and output built-in office chests from all selectable chest lists.
B) Exclude only the input chest; allow the output chest to be selected explicitly.
C) Exclude only the output chest; allow the input chest to be selected explicitly.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: B

## Question 5
Which testing emphasis should U-MC-02 carry into code generation?

A) Example tests plus PBT for backfill idempotence and chest-exclusion invariants.
B) Example tests only, because live Stardew building/chest APIs make useful PBT impractical here.
C) PBT only for pure coordinate/identity helpers, with live API behavior covered by examples.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: B
