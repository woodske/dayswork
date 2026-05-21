# AI-DLC State Tracking — Dayswork

## Project Information
- **Project Name**: Dayswork
- **Description**: Stardew Valley SMAPI mod that lets the player hire generic worker NPCs from the bulletin board
- **Project Type**: Greenfield
- **Start Date**: 2026-05-18
- **Current Phase**: CONSTRUCTION
- **Current Stage**: U-14 Output Pipeline — **Functional Design APPROVED 2026-05-21 (all 7 FD questions answered A); NFR Requirements complete, awaiting approval.** FD artifacts at [u-14-output-pipeline/functional-design/](construction/u-14-output-pipeline/functional-design/); NFR artifacts at [u-14-output-pipeline/nfr-requirements/](construction/u-14-output-pipeline/nfr-requirements/). Design: buffer tags items with TaskKind → pure DepositPlanner resolves via Contract.TaskDestinations → one nearest-ordered trip per chest/bin → chest-full/missing/unassigned/sleep-interrupt route to a single no-fee MFM overflow letter (body lists each reason) + separate vanilla tool-missing warning letter; refund/exit unchanged; MFM required dependency; no custom mail save data. Previous: U-13B — Code Generation APPROVED 2026-05-21. FD + NFR Requirements + NFR Design approved 2026-05-21. Code plan at [u-13b-farmer-worker-tool-visuals-code-generation-plan.md](construction/plans/u-13b-farmer-worker-tool-visuals-code-generation-plan.md) — original 14 steps complete plus review-change Steps 15–23. User approved the final NPC-backed worker architecture after play-testing. Farmer-backed worker was tried, then intentionally rejected due to standalone `Farmer` rendering depth, body-pose, movement, and vanilla tool-callback/null-reference risk. Current accepted approach is an NPC worker added to the normal farm character list, with explicit Dayswork task effects plus Stardew-Squad-style callback-free NPC task animation and world `TemporaryAnimatedSprite` tool swings for axe/pickaxe/watering-can/scythe tasks. Preserved/fixed behavior now includes nearest outdoor routing, resource-clump handling, navigation/action SMAPI diagnostics, worker-passable BFS fallback that routes around farm buildings, deterministic regular stone removal, material chunk debris collection for wood/hardwood/stone/ore drops, delayed tree-fall debris sweeps for wood spawned after the fall animation, a short visible morning entrance hold, visible final exit walk past the farm entrance after deposit, menu/time pause gating, and repeated axe swings until vanilla tree removal completes. `dotnet build`: 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test`: 184 passed, 1 expected skipped. Next → U-14 Output Pipeline construction loop.
- **Unit split (2026-05-21)**: U-13 split into **U-13 (Worker AI)** — priority/skip/capability/stuck/invulnerability on the existing NPC + real walking — and **U-13B (Worker Actor + Task Visuals)** — initially explored full-Farmer re-founding, then pivoted back to NPC after review/play-test feedback. U-13B runs immediately after U-13, then U-14 → U-15 → U-16. Rationale: isolate high-uncertainty worker actor/visual integration from lower-risk worker-AI logic. See [unit-of-work.md](inception/application-design/unit-of-work.md) U-13/U-13B entries.

## Open TODOs
- **TODO-01** [U-11 / tree drops]: Tree seeds were not observed in the shipping bin after felling trees. Suspected cause: the worker teleports between work tiles so fast that seeds spawned as debris may land after the farmhand has already moved on. Accepted for now. **Revisit after U-13 (Worker AI) slows the worker down to a realistic pace — confirm seeds appear then. If still missing, investigate CollectNewDebris snapshot timing.**
- **TODO-02** [U-12 / dead crops]: Do not water dead crops; add optional task to clear dead crops. Defer to a future unit — requires distinguishing live vs. dead crop tiles and a new TaskKind (ClearDeadCrops).
- **TODO-03** [U-12 / zone zoom]: Add zoom-out capability in the zone selection screen so players can see the full farm when selecting harvest/work zones. Defer to a future unit — requires viewport camera manipulation or a minimap overlay approach.
- **TODO-04** [U-15 / multiple contracts]: Define priority rules for concurrent contracts (e.g., recurring + one-time conflict, multiple recurring contracts targeting the same tasks). Scheduled for U-15 (Recurring Contract Lifecycle) which already covers deposit deduction, festival skip, and can't-afford mail. Add priority/conflict resolution to that unit's scope.
- **TODO-05** [U-13 / animals + buildings]: Animal tasks (Feed animals, Pet animals, Collect animal products) and **all building-interior work — including the greenhouse** — are deferred out of U-13 per FD-Q1=A. Requires building-door warp navigation (FR-WORK-09) plus the three animal task actions. Needs its own unit (e.g., "Animals & Buildings"). The FR-WORK-03 priority order already reserves slots for the animal tasks; only detection/invocation/navigation are missing.
- **TODO-06** [Future / custom worker art + richer tool visuals]: Farmer-backed rendering was removed and the worker is back in the normal NPC draw path, resolving the `RenderedWorld` foreground-depth caveat for the current placeholder worker. Directional world tool-swing sprites now spawn during active task beats. Future polish can add custom worker sprites and richer per-tool NPC animations/overlays if the placeholder Marnie-style task beat is not expressive enough.

## Decisions / Deviations
- **DEV-01** [U-13B / worker actor decision]: Farmer re-founding was implemented and play-tested, then rejected due to standalone `Farmer` rendering depth issues, body-pose corruption risk, movement mismatches, and vanilla tool animation callbacks that assume real player state. Accepted direction is an NPC-backed worker in the normal farm character list, with explicit Dayswork task execution and custom callback-free task animation. Visible tools are represented by world `TemporaryAnimatedSprite` swings inspired by Stardew Squad rather than by invoking full vanilla Farmer tool animations. Post-v1 worker energy/tools/food/buffs should be modeled explicitly rather than depending on hidden `Farmer` behavior.
- **DEV-02** [U-13 / FR-WORK-03]: Outdoor work routing now uses greedy nearest-next selection across currently detected non-animal tasks instead of fixed task-kind priority among grass/weeds/rocks/trees/crops. Distance is measured to the task tile, while the worker's current physical position remains the last navigation tile. Detection checks placed objects/resource clumps before grass so grass cannot mask rocks/weeds/twigs on the same tile. Blocked targets resolve to a reachable orthogonal stand tile; walkable targets use the task tile with an orthogonal fallback when the task tile is not passable. Resource clumps now classify into axe/pick work using Stardew constants (`stumpIndex`, `hollowLogIndex`, `boulderIndex`, `meteoriteIndex`, etc.), search for a stand tile around the whole clump footprint, and are removed by the matching invocation handlers. Regular stone objects are revalidated as pick targets and removed deterministically after the simulated pickaxe action so a false `performToolAction` return cannot silently complete a shift without clearing accepted rocks. User play-test feedback: worker should perform whichever thing is closest, with animal tasks as the future exception. Animal tasks remain deferred to TODO-05 and can regain first-priority handling when implemented.

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
