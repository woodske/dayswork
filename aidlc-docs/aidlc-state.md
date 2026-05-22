# AI-DLC State Tracking — Dayswork

## Project Information
- **Project Name**: Dayswork
- **Description**: Stardew Valley SMAPI mod that lets the player hire generic worker NPCs from the bulletin board
- **Project Type**: Greenfield
- **Start Date**: 2026-05-18
- **Current Phase**: CONSTRUCTION
- **Current Stage**: U-14 Output Pipeline — **Code Generation COMPLETE 2026-05-21, awaiting playtesting/approval. NEXT -> U-15 Recurring Lifecycle (or Build & Test once all units done).**
  - **Latest U-14 review**: Code plan now includes review/playtest-fix Steps 17-37 at [u-14-output-pipeline-code-generation-plan.md](construction/plans/u-14-output-pipeline-code-generation-plan.md); code summary updated at [u-14-output-pipeline/code/code-summary.md](construction/u-14-output-pipeline/code/code-summary.md). Installed Mail Framework Mod confirmed at `X:\Steam\steamapps\common\Stardew Valley\Mods\MailFrameworkMod`: UniqueID `DIGUS.MailFrameworkMod`, version `1.20.0`, API shape `RegisterLetter(ILetter, Func<ILetter,bool>, Action<ILetter>, Func<ILetter,List<Item>>)`. The guessed stub was replaced with `MailFrameworkModApiAdapter`; `manifest.json` now requires MFM `1.20.0`.
  - **Latest playtest fix**: User confirmed tree output reached 17 wood, then reported standard rocks removed without collected stone and a remaining empty worker mail. Stardew `Object.performToolAction` IL inspection showed standard rock breakage emits radial visual chunk debris without an explicit item id, so Dayswork's no-foreign-material debris filter rejected it. `ShiftOrchestrator` now converts only a removed standard Stone object into exactly 1 `(O)390` Stone when no explicit item-bearing debris was collected; it still refuses unlabeled `chunkType` material inference, so copper/wood/ore cannot appear unless Stardew supplies explicit item metadata. `MailDispatcher` now logs queued/registering mail IDs and attachment counts, and `MailFrameworkModApiAdapter` always supplies MFM `dynamicItems`, including no-attachment warning letters, for empty-mail diagnosis.
  - **Verification**: `dotnet build Dayswork.sln`: 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln`: 190 passed, 1 expected skip. Reflection smoke check previously created the MFM adapter and reached MFM's real `RegisterLetter`; standalone failure was expected (`Can't add a letter before the game is launched`).
  - **U-14 design**: buffer tags items with TaskKind (Pattern L) -> pure DepositPlanner resolves via Contract.TaskDestinations + nearest-neighbor Manhattan oracle (Pattern M) -> multi-trip deposit loop via SetIntent, no new phase, new IntentDepositAtChest (Pattern N) -> chest-full/missing/unassigned/sleep-interrupt accumulate to ShiftContext.Overflow -> single no-fee MFM overflow letter, body lists each reason (Pattern O) + separate MFM text-only tool-missing warning letter via MailDispatcher (Pattern P). Refund/exit unchanged; no custom mail save data; MFM letter condition enforces delivery after the queued day. Deviations: DEV-U14-01, DEV-U14-02, DEV-U14-03. Infrastructure Design SKIPPED (no cloud/IaC).
  - **Previous**: U-13B Code Generation APPROVED 2026-05-21. Next -> U-14 playtesting/approval.
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
- **DEV-U14-01** [U-14 / FR-OUT-05, FD-Q7]: Tool-missing warning mail is sent through MFM as a **text-only (no-attachment)** letter rather than vanilla `Game1.addMailForTomorrow` (the FD-Q7=A / plan wording). Reason: vanilla custom mail can't cleanly carry per-shift dynamic text (the skipped-task list) and re-deliver daily; MFM (already a required dependency) handles text-only letters cleanly. FD-Q7=A's behavioural intent (one separate combined no-item warning per shift, listing skipped tasks) is preserved.
- **DEV-U14-02** [U-14 / NFR-SAFE-01]: `ShiftOrchestrator.OnSaving` mid-work branch (player sleeps while worker still working, `ShiftEndTime` unset) now **mails the collected items** instead of discarding them, while keeping the existing full-deposit refund. Proper sleep fast-forward (billing nuance, festival/rain) remains U-15 scope.
- **DEV-U14-03** [U-14 / MFM integration]: The guessed `IMailFrameworkModApi.RegisterLetter(id, synopsis, text, attachments)` stub was replaced after inspecting the installed Mail Framework Mod at `X:\Steam\steamapps\common\Stardew Valley\Mods\MailFrameworkMod`. Confirmed MFM `1.20.0` exposes `RegisterLetter(ILetter, Func<ILetter,bool>, Action<ILetter>, Func<ILetter,List<Item>>)`. Dayswork now uses `MailFrameworkModApiAdapter` to fetch the raw API object via `GetApi("DIGUS.MailFrameworkMod")`, create MFM `Letter`/`ApiLetter` instances by reflection, and pass a deliver-after-queued-day condition. `manifest.json` now declares `MinimumVersion` `1.20.0`. If binding/registration fails, `MailDispatcher` still falls back (overflow → shipping bin, warning → log) so no items are lost.
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
