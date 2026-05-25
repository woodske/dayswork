# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Functional Design Plan

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Stories**: S-05, S-12, S-19  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Answers reviewed, no clarification round needed, and functional-design artifacts generated. Pending user review.

---

## Plan Checklist

- [x] Load unit definition, refreshed requirements, refreshed stories, refreshed application design, and current persistence implementation
- [x] Collect answers to FD-Q1 through FD-Q8
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Present completion message and await approval

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-19 definition and definition of done
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — story ownership for S-05, S-12, S-19
- [requirements.md](../../inception/requirements/requirements.md) — fixed contract pricing, recurring stability, and legacy cleanup requirements
- [stories.md](../../inception/user-stories/stories.md) — player-facing expectations around saved contracts, editing, and recurring behavior
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary and deferred persistence details
- [component-methods.md](../../inception/application-design/component-methods.md) — `IContractStore` and `ISaveDataSerializer` target seams
- [component-dependency.md](../../inception/application-design/component-dependency.md) — persistence and recurring data-flow expectations
- `Dayswork.Core/Domain/Contract.cs` — current additive bridge shape
- `Dayswork.Core/Persistence/SaveDataSerializer.cs` — current schema v1 serializer behavior
- `Dayswork.Core/Persistence/Dto/ContractDtoV1.cs` and `Dayswork.Core/Persistence/Dto/DaysworkSaveDataV1.cs` — current persisted shape
- `Dayswork/Integration/ContractPersistenceAdapter.cs` — SMAPI save bridge
- `Dayswork.Core/Persistence/IContractStore.cs` and `ContractStore.cs` — current in-memory persistence surface
- `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs` and `ContractStoreTests.cs` — current regression coverage

---

## What This Unit Must Define

U-19 is the persistence retrofit that carries the U-18 contract-terms model into saved game state and removes the unreleased hourly-contract save path.

This unit owns or rewrites:
- current-schema persisted contract DTO shape
- save-envelope versioning and legacy-load behavior
- persisted representation of `ContractScopeSelection`
- persisted representation of `ContractTermsSnapshot`
- contract-store seams needed to replace saved terms snapshots safely
- malformed-save handling rules for current-schema contracts

This unit extends:
- `C-15 ContractStore`
- `C-16 SaveDataSerializer`
- `M-15 ContractPersistenceAdapter`
- persisted `Contract` state shape

---

## Already Decided And Not Re-Decided Here

- One-time contracts preserve the exact terms snapshot confirmed at hire time.
- Recurring contracts rebuild terms from saved scope and current config on the next eligible day.
- Legacy pre-release hourly/deposit/refund contracts are dropped instead of migrated.
- The drop is not explained to the player with mail, UI, or other player-facing messaging.
- Task destinations, schedule shape, and pause/cancel semantics are not being redesigned here; this unit only decides how those existing concepts persist alongside the new scope/terms model.
- Current runtime consumers still exist on the historical pricing path in later retrofit units, so this unit may need an explicit bridge strategy rather than assuming every downstream consumer has already switched.

This plan focuses only on the remaining functional-design choices that shape the persistence contract for the redesign.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

### FD-Q1 — What should be the authoritative saved pricing shape for a current-schema contract?

We now have two kinds of pricing state: raw selected scope and the computed `ContractTermsSnapshot`. We need to decide what is persisted for one-time and recurring contracts.

A) **Persist both scope selection and terms snapshot for every current-schema contract (Recommended)** — one-time contracts execute the saved snapshot unchanged; recurring contracts also keep the latest saved snapshot, but that snapshot can be replaced later by a rebuild from the saved scope.

B) **Persist both only for one-time contracts; recurring contracts store raw scope only** — recurring contracts always rebuild from scope and never persist a snapshot.

C) **Persist raw scope only for every contract** — both one-time and recurring contracts rebuild their terms later instead of trusting a saved snapshot.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q2 — How should the selected work scope be serialized in the current-schema contract DTO?

Historical persistence stores only `Zones`, which is no longer expressive enough for outdoor zones plus animal buildings plus greenhouse scope.

A) **Persist a typed scope DTO explicitly (Recommended)** — current-schema save data has first-class fields for outdoor zones, selected animal-building references, and greenhouse selection instead of encoding them through legacy zone conventions.

B) **Keep the legacy `Zones` field and encode everything through conventions** — continue using placeholders such as giant whole-interior zones to imply barns/coops and greenhouse scope.

C) **Hybrid shape** — keep `Zones` for outdoor areas, but add only minimal extension fields for the new non-zone scope types.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q3 — How should legacy hourly-contract detection work in save-data versioning?

The exact legacy-drop heuristic was deferred from Application Design to this unit.

A) **Bump the save envelope to a new schema version and treat older envelopes as legacy pre-release data (Recommended)** — redesigned contracts save as a new schema version, and legacy schema v1 envelopes load as no contracts with no player-facing explanation.

B) **Keep one schema version and infer legacy-vs-current per contract shape** — the serializer inspects contract fields to decide whether each contract is old hourly data or new snapshot data.

C) **Support both legacy and current contract shapes in parallel** — older hourly contracts remain readable, but are dropped individually rather than by envelope version.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q4 — How should malformed current-schema contracts behave during load?

Even after legacy cleanup, a current-schema save can still contain partial or malformed contract entries.

A) **Skip malformed current-schema contracts individually and keep valid ones (Recommended)** — valid current-schema contracts still load, while bad entries are dropped with maintainer-facing diagnostics.

B) **Fail the whole load if any current-schema contract is malformed** — one bad entry prevents all contracts from loading.

C) **Auto-heal malformed contracts with defaults where possible** — missing fields are synthesized instead of dropping the contract.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q5 — What persistence seam should own post-save repricing for recurring contracts?

Application Design introduced `ReplaceTermsSnapshot(...)` as a target seam, but the current store still exposes only generic whole-contract `Update(...)`.

A) **Add a dedicated `ReplaceTermsSnapshot` seam to the store (Recommended)** — recurring repricing and approved edits can update saved terms explicitly without forcing every caller to rebuild unrelated contract fields.

B) **Keep only whole-contract `Update(...)`** — callers always replace the full `Contract` whenever terms need to change.

C) **Use a generic patch/mutation API instead** — add a broader mutation seam rather than one explicit terms-replacement method.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q6 — During the retrofit bridge, should current-schema persistence keep legacy financial fields too?

Some runtime consumers still read `DepositAmount` and `HourlyRate` until later retrofit units switch them over.

A) **Keep legacy financial fields temporarily alongside the new saved scope/terms data (Recommended)** — the current-schema save becomes a bridge shape that carries both the redesign data and the temporary historical fields until downstream consumers are migrated.

B) **Drop legacy financial fields immediately in the new schema** — current-schema persistence carries only the redesign data, and any remaining consumers must be switched in this unit.

C) **Keep legacy financial fields only for recurring contracts** — one-time contracts persist only the redesign shape, while recurring contracts remain bridged.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q7 — When should a recurring contract’s persisted snapshot be refreshed after the contract already exists?

Once recurring contracts persist both scope and the latest known terms snapshot, we still need to decide when that saved snapshot is replaced.

A) **Refresh it immediately whenever an approved edit or successful recurring rebuild produces new terms (Recommended)** — editing a recurring contract saves the new scope plus the new preview snapshot now, and day-start repricing can replace that snapshot again when it rebuilds from current config.

B) **Refresh it only at day-start when the recurring contract actually activates** — edits save new scope, but the saved snapshot stays stale until the next eligible activation.

C) **Never persist recurring snapshot refreshes after creation** — recurring contracts keep their original saved snapshot forever and rebuild only in memory.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q8 — What logging policy should apply to silently dropped legacy data and malformed current-schema entries?

The player should not get an explanation, but maintainers may still benefit from diagnostics.

A) **No player-facing message, but keep maintainer-facing diagnostics in the log (Recommended)** — legacy envelopes and malformed current-schema contracts are dropped quietly from gameplay, while the serializer logs what happened for debugging.

B) **Completely silent at every layer** — no player-facing message and no maintainer-facing diagnostics.

C) **Player-facing explanation plus logs** — dropped contracts generate in-game mail, UI text, or another visible explanation.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/functional-design/business-rules.md`
