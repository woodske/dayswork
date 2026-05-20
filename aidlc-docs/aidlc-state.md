# AI-DLC State Tracking — Dayswork

## Project Information
- **Project Name**: Dayswork
- **Description**: Stardew Valley SMAPI mod that lets the player hire generic worker NPCs from the bulletin board
- **Project Type**: Greenfield
- **Start Date**: 2026-05-18
- **Current Phase**: CONSTRUCTION
- **Current Stage**: U-13 Worker Features: Priority + Stuck + Tool Swap + Invulnerability — **Functional Design complete; awaiting approval to proceed to NFR Requirements.**

## Open TODOs
- **TODO-01** [U-11 / tree drops]: Tree seeds were not observed in the shipping bin after felling trees. Suspected cause: the worker teleports between work tiles so fast that seeds spawned as debris may land after the farmhand has already moved on. Accepted for now. **Revisit after U-13 (Worker AI) slows the worker down to a realistic pace — confirm seeds appear then. If still missing, investigate CollectNewDebris snapshot timing.**
- **TODO-02** [U-12 / dead crops]: Do not water dead crops; add optional task to clear dead crops. Defer to a future unit — requires distinguishing live vs. dead crop tiles and a new TaskKind (ClearDeadCrops).
- **TODO-03** [U-12 / zone zoom]: Add zoom-out capability in the zone selection screen so players can see the full farm when selecting harvest/work zones. Defer to a future unit — requires viewport camera manipulation or a minimap overlay approach.
- **TODO-04** [U-15 / multiple contracts]: Define priority rules for concurrent contracts (e.g., recurring + one-time conflict, multiple recurring contracts targeting the same tasks). Scheduled for U-15 (Recurring Contract Lifecycle) which already covers deposit deduction, festival skip, and can't-afford mail. Add priority/conflict resolution to that unit's scope.
- **TODO-05** [U-13 / animals + buildings]: Animal tasks (Feed animals, Pet animals, Collect animal products) and **all building-interior work — including the greenhouse** — are deferred out of U-13 per FD-Q1=A. Requires building-door warp navigation (FR-WORK-09) plus the three animal task actions. Needs its own unit (e.g., "Animals & Buildings"). The FR-WORK-03 priority order already reserves slots for the animal tasks; only detection/invocation/navigation are missing.

## Decisions / Deviations
- **DEV-01** [U-13 / FR-NPC-01]: Worker re-founded on `Farmer` (randomized character-creation appearance) instead of the recolored-NPC placeholder, to deliver authentic player-style tool animations (FR-WORK-10/S-07) and to make the post-V1 roadmap (energy bar, worker-owned tools, food/buffs — all Farmer-native) tractable. Trade-off accepted: U-13 rebuilds movement (custom path-follower, not PathFindController), depth-sorted drawing, and manual hit-detection for the ouch emote. Dialogue becomes additive custom work later. Recorded in U-13 functional-design business-rules.md.

## Workspace State
- **Existing Code**: No
- **Programming Languages**: (to be: C# / .NET targeting Stardew Valley + SMAPI)
- **Build System**: (to be: MSBuild / `dotnet`)
- **Project Structure**: Empty (greenfield)
- **Reverse Engineering Needed**: No
- **Workspace Root**: `C:\Users\kwood\Repos\dayswork`

## Code Location Rules
- **Application Code**: Workspace root (NEVER in `aidlc-docs/`)
- **Documentation**: `aidlc-docs/` only
- **Source spec**: `aidlc-docs/inception/source-spec.md` (copy of user-provided design doc)

## User Profile (informs onboarding-level docs)
- Experienced software engineer
- New to C# / .NET
- New to Stardew Valley modding / SMAPI ecosystem
- Implication: tech stack rationale, project scaffolding, and SMAPI conventions need to be documented explicitly during Inception and Construction.

## Extension Configuration
| Extension | Enabled | Mode | Decided At |
|---|---|---|---|
| Security Baseline | No | — | Requirements Analysis (Q28: B — no network/PII/auth surface) |
| Property-Based Testing | Yes | Partial — enforces PBT-02, PBT-03, PBT-07, PBT-08, PBT-09; remainder advisory | Requirements Analysis (Q29: B) |

**PBT framework**: FsCheck (per PBT-09 recommendation for C#/.NET; integrates with xUnit chosen in Q4).

## Stage Progress
### INCEPTION PHASE
- [x] Workspace Detection
- [ ] Reverse Engineering (N/A — greenfield)
- [x] Requirements Analysis (approved 2026-05-18)
- [x] User Stories (approved 2026-05-18)
- [x] Workflow Planning (approved 2026-05-18)
- [x] Application Design (approved 2026-05-18, with live-docs verification addendum)
- [x] Units Generation — approved 2026-05-18

### CONSTRUCTION PHASE (per-unit loop)
- [ ] Functional Design — **EXECUTE** (per unit)
- [ ] NFR Requirements — **EXECUTE** (per unit)
- [ ] NFR Design — **EXECUTE** (per unit)
- [ ] Infrastructure Design — **SKIP** (no cloud / container / IaC; SMAPI is the platform)
- [ ] Code Generation — **EXECUTE** (per unit, always)
- [ ] Build and Test — **EXECUTE** (after all units complete)

### OPERATIONS PHASE
- [ ] Operations — **PLACEHOLDER** (v1 ships without deployment automation)
