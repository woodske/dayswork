# Domain Entities — U-WR Worker Routing and Dynamic Task Selection

## Overview

These are logical domain entities for the worker-routing design. They do not require new persisted data. Implementation may represent them as records, local helper types, or methods as long as the behavior remains testable.

## Entity: WorkCandidate

Represents one unit of selectable work inside the active broad batch.

| Field | Meaning |
|---|---|
| `CandidateId` | Stable identifier for deterministic ordering within one batch pass. |
| `TaskKind` | The task represented by the candidate. |
| `Family` | Tile, animal, feed hopper, or feed trough. |
| `TaskTile` | The tile affected by tile/feed work, when applicable. |
| `AnimalRef` | The target animal, when applicable. |
| `InteractionTiles` | Candidate stand tiles where the worker can perform the work. |
| `Provenance` | Existing output provenance for routing collected items. |
| `StableOrder` | Scan/discovery order used after task-priority ties. |

### Notes

- A tile candidate can have multiple interaction tiles.
- An animal candidate can have one or more interaction tiles around or at the animal's current position.
- A feed trough candidate is actionable only after hay is in hand.

## Entity: InteractionRoute

Represents the route evaluation result for one interaction tile.

| Field | Meaning |
|---|---|
| `InteractionTile` | The tile the worker would walk to. |
| `Reachable` | Whether the worker can path there now. |
| `RouteCost` | Actual path length from the worker's current tile if reachable. |

### Notes

- Unreachable routes are excluded from selection.
- Route cost zero means the worker is already standing on the interaction tile.

## Entity: SelectableWork

Represents a work candidate after its best reachable interaction route has been selected.

| Field | Meaning |
|---|---|
| `Candidate` | The underlying work candidate. |
| `BestRoute` | The lowest-cost reachable interaction route. |
| `PriorityRank` | Existing task-priority rank. |
| `StableOrder` | Stable tie-break value. |

## Entity: ActiveBatchWorkSet

Represents all candidate state for the current broad batch.

| Field | Meaning |
|---|---|
| `BatchKind` | Animal building, outdoor animals, greenhouse, outdoor crops, or outdoor clearing. |
| `LocationName` | The current Stardew location name. |
| `Candidates` | Current actionable candidates. |
| `Deferred` | Candidates blocked earlier in the batch. |
| `HayInHand` | Current feed hay count for animal-building batches. |
| `ProgressInPass` | Whether the current pass changed world state. |

## Entity: SelectionResult

Represents the selector outcome at a task boundary.

| Outcome | Meaning |
|---|---|
| `Selected` | A reachable candidate was chosen. |
| `Deferred` | Candidate was blocked and moved to deferred state. |
| `CompleteBatch` | No reachable or retryable candidates remain. |
| `SkipBlockedRemainder` | A no-progress pass ended retry for blocked candidates. |

## Entity: DeferredWork

Represents a candidate that should be retried later in the same active batch.

| Field | Meaning |
|---|---|
| `Candidate` | The blocked work candidate. |
| `BlockedReason` | No interaction route, navigation failed, stale target, or prerequisite missing. |
| `FirstDeferredAtPass` | Pass number when deferral began. |
| `LastCheckedAtPass` | Most recent pass number when it was re-evaluated. |

## Entity: BatchProgressEvent

Represents an event that can unlock another retry pass.

| Event | Counts as progress |
|---|---|
| Task completed | Yes |
| Animal petted | Yes |
| Animal product collected | Yes |
| Hay collected from hopper | Yes |
| Hay placed in trough | Yes |
| Object/tile state changed | Yes |
| Worker moved only | No |
| Candidate checked but still blocked | No |

## No Persistence Changes

This unit introduces no saved data and no player-facing configuration. All routing state is per-shift or per-active-batch runtime state.

## Frontend/UI Artifact

Frontend/UI design is N/A for this unit. The change is visible through worker behavior, but it does not introduce menus, screens, controls, localization strings, or UI state.

## Testable Properties

| Entity or operation | Property category | Property |
|---|---|---|
| `WorkCandidate` to `SelectableWork` | Invariant | Candidates with no reachable route are not selectable. |
| Selector over `SelectableWork` | Invariant | Selected work has minimum route cost and deterministic tie-break order. |
| `InteractionRoute` | Invariant | Route cost is non-negative; zero cost means the worker is already on the interaction tile. |
| `ActiveBatchWorkSet` deferral pass | Invariant | A no-progress pass ends retry for blocked candidates. |

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant - domain entities identify properties for PBT-01 and carry them forward to code-generation planning. |
