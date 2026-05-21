# U-13B — Farmer Worker + Tool Visuals: NFR Requirements Plan

**Unit**: U-13B — Farmer Worker + Tool Visuals
**Phase**: CONSTRUCTION — NFR Requirements

---

## Plan Checklist

- [x] Analyze functional design artifacts
- [x] Pull applicable NFRs from requirements.md per unit scope (incl. the Farmer NFRs deferred from U-13)
- [x] Determine PBT obligations (PBT extension — Partial mode)
- [x] Record tech-stack decisions
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

All U-13B NFRs are determinable from the approved Functional Design + prior project decisions (consistent with how U-07, U-10, and U-13 NFR Requirements were handled):

- **Performance** budget, tick-throttle, and once-per-shift scan are inherited from U-10/U-13 and unchanged in principle. U-13B adds the two Farmer-specific performance NFRs that U-13 explicitly deferred: per-frame `FarmerRenderer` draw and per-tick manual movement stepping — both bounded and cheap (the game already runs `FarmerRenderer` for the player + remote farmhands).
- **Safety/reliability** follow directly from the Farmer-not-serialized and behaviour-preservation rules already fixed in U-13B's business-rules.md (BR-WORKER-01, BR-PRESERVE-01).
- **Tech stack** adds **no new frameworks** — the Farmer rendering/movement/appearance use existing Stardew APIs (`Farmer`, `FarmerRenderer`, `FarmerSprite`, `PathFindController` for path-compute-only); testing stays on xUnit + FsCheck. The only new Core type, `WorkerTool`, is a pure enum + map, unit-tested (table-driven), not a new PBT obligation.
- The one genuinely open *engineering* choice — **movement smoothness cadence** (step `Farmer.Position` every tick at ~60 Hz for a glassy walk vs. step on the throttled ~15 Hz sample and accept slight choppiness vs. render-side interpolation between sampled positions) — is a **pattern decision for NFR Design**, not a product preference. Recorded as a deferred tech decision (mirrors how U-13 deferred the render-hook-vs-`location.characters` choice), to be resolved in U-13B NFR Design with a code-gen play-test confirm.

---

## Applicable NFRs (detail in nfr-requirements.md)
- **Performance**: NFR-PERF-01 (per-frame budget — bounded Farmer draw + O(1) movement steps), NFR-PERF-02 (path computed per-target, not per-tick).
- **Safety**: NFR-SAFE-03 (no save corruption — Farmer never serialized; carried from U-13 SAFE-U13-03, the one Farmer NFR that already applied to both). NFR-SAFE-01/02/04 are preserved by BR-PRESERVE-01 (unchanged deposit/refund/debris logic) — restated as preservation guarantees, not re-implemented.
- **Reliability**: graceful no-path skip (retained), renderer/movement no-op when no worker active, appearance generation never produces invalid sprite indices.
- **Maintainability**: NFR-MAINT-03 (`WorkerTool` pure Core, zero Stardew refs), NFR-MAINT-02 (FsCheck retained), NFR-MAINT-04 (no new Harmony patches — render via SMAPI event, not a draw patch, per FD-Q2=A), NFR-UX-02 (i18n — no new user-visible strings this unit).
- **PBT (Partial mode)**: PBT-03 applies only to the pure `WorkerTool.ForTask` map, which is fully covered by an exhaustive table test rather than a property (finite, total mapping) → PBT effectively **N/A** for new properties this unit; PBT-08 seed-logging convention still honored if any property is added. PBT-02/07/09 N/A.
