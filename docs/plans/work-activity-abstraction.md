# Plan — Work-activity abstraction (orchestrator mode dispatch)

**Status:** proposed 2026-07-07, not started. Item #4 in
[architecture-review-index.md](architecture-review-index.md). **Gate cleared 2026-07-07:** the
pending smoke-pass backlog (travel/orchestrator-decomposition, serpentine, machines cross-location
fetch, fish ponds) all passed in-game. Land this **before the next "Manage X" feature**.

## Problem

Every activity the worker performs shares one lifecycle — *pick next step → navigate → on-arrive
perform → advance; on nav-failure skip/defer* — but each is wired in by hand:

- `ShiftOrchestrator.Movement.cs` `HandleMovement` is two priority-ordered if-chains
  (arrival + failure) over `MachinesActive`/`MachineFetchPending` → `FishPondsActive` →
  `ManagedActive` → `CurrentAnimalWork` → `CurrentTileWork`.
- `ShiftSession` carries parallel per-feature state: `CurrentMachineStep` / `CurrentFishPondStep`
  / `CurrentManagedAction` plus their active/pending booleans, whose mutual exclusion is
  maintained by convention only.
- `ShiftOrchestrator.Travel.cs` dispatches `TravelPurpose` arrivals/failures in a second switch
  that must know every activity's continuation method.

Machines and fish ponds each added a branch to *every* dispatch point; the next feature will too.
This clears the codebase's own no-ceremony bar: there are **5+ concrete implementations** of the
same shape (tile work, animal work, managed crops, machines, fish ponds — plus the already-
object-shaped shopping coordinator and deposit runner).

## Design sketch

### The interface (internal, `Dayswork/Orchestration/`)

```csharp
internal interface IWorkActivity
{
    /// Start or continue; false = activity exhausted, orchestrator advances the batch/plan.
    bool TryStartNextStep();
    void OnArrived(GameLocation location);
    void OnNavigationFailed(GameLocation location);
}
```

`ShiftSession` holds **one** `IWorkActivity? ActiveActivity` in place of the per-feature
current-step fields and mode booleans. `HandleMovement` collapses to:

```
if (_nav.NavigationFailed) { Session.ActiveActivity?.OnNavigationFailed(loc); return; }
if (_nav.HasArrived)       { Session.ActiveActivity?.OnArrived(loc); }
```

Activities own their step state internally (e.g. `MachineActivity` absorbs
`CurrentMachineStep`/`MachineFetchPending`/`CurrentMachineReload`), created per batch and
discarded with it — same "fresh object is the reset" idiom as `ShiftSession` itself.

### Migration order (each phase builds clean + smoke-checks before the next)

1. **Machines** — most branches, most session flags; biggest payoff, best proof of the shape.
2. **Fish ponds** — smallest activity; near-mechanical after 1.
3. **Managed crops** — includes its interaction with the shopping coordinator.
4. **Tile + animal work** — either becomes the default `BatchWorkActivity` or explicitly stays on
   the legacy path with a comment; decide at phase 3 based on how much of
   `StartNextAnimalOrTileOrAdvance` naturally fits the interface. Don't force it.
5. **Optional:** align `ManagedShoppingCoordinator` and `DepositTripRunner` surfaces to the
   interface (they are already objects; this is renaming/thinning, not rewriting), and fold the
   `TravelPurpose` switch into per-activity travel continuations. Separate change, not a
   prerequisite — `TravelPurpose` dispatch can coexist with activities indefinitely.

## What must NOT change

- **Behavior.** This is a structure-only refactor: same step order, same defer/skip semantics,
  same energy spends, same sounds, same deposit routing (hard rule 4 paths untouched).
- All worker beats keep routing through `RunGuardedWorkerBeat` / `InvokeTaskActionGuarded` — the
  unified-guard rule; an activity must never grow its own vanilla-API call path.
- `ShiftSession` remains the single home of per-shift mutable state; activities live on it.
- The stuck detector, travel failure policies, and wrap-up gates keep their current hook points.

## Testing

Per testing policy the shift engine is play-test verified: each migration phase gets an in-game
smoke pass of that activity (machines: collect/reload + cross-location fetch; ponds: collect;
managed: full lifecycle day). Unit tests only where an activity's step-planning is already pure
(machine input planning etc. — existing tests keep passing unchanged, which is itself the signal).

## Open questions

- Does `IWorkActivity` need an `OnTravelArrived(TravelPurpose)` member in v1, or do activities
  register continuations when they start a travel? (Leaning: keep `TravelPurpose` switch in v1,
  fold later — smaller diff per phase.)
- Where does `BatchSelectionAttempts`-style loop protection live — per-activity or stay on the
  session as a shared guard? (Leaning: shared; it protects the orchestrator, not the activity.)
