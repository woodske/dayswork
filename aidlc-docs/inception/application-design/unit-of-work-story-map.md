# Unit of Work — Story Map for Pricing Model Redesign Retrofit

This document maps the refreshed story set to the appended retrofit units `U-18` through `U-24`.

Historical units `U-01` through `U-17` still explain how the original system was built. This map explains which retrofit unit now changes or regression-verifies each refreshed story under the pricing redesign.

**Reading the table**:
- **Historical baseline** = where the story originally landed before the redesign
- **Primary retrofit unit** = the main redesign unit responsible for changing or re-validating that story now
- **Supporting retrofit units** = other redesign units that deepen or verify the story

---

## Story-to-retrofit map

### Section 1 — Discovery & First Hire

| Story | Historical baseline | Primary retrofit unit | Supporting retrofit units | Retrofit role |
|---|---|---|---|---|
| `S-01` Discover the hiring option on the bulletin board | `U-08` | `U-20` | `U-24` | Board entry still exists, but the opened flow now lands in the redesigned preview/confirmation pipeline; final regression verifies multiplayer and entry behavior still hold |
| `S-02` Configure tasks and see the live contract price | `U-09` | `U-20` | `U-18` | Core pricing/terms model changes in `U-18`; player-facing live preview ships in `U-20` |
| `S-03` Draw zones and select buildings on the farm | `U-11`, `U-16` | `U-20` | `U-18`, `U-22` | UI/preview semantics change in `U-20`; typed-scope foundation lands in `U-18`; runtime alignment for those selections completes in `U-22` |
| `S-04` Assign output destinations per task | `U-11`, `U-14`, `U-16` | `U-22` | `U-24` | Destination behavior must remain correct under typed scopes; final regression/docs pass confirms unchanged mail/deposit behavior |
| `S-05` Choose a one-time or recurring schedule | `U-09`, `U-12`, `U-15` | `U-20` | `U-19`, `U-23` | UI/preview language changes in `U-20`; persistence semantics in `U-19`; recurring daily behavior in `U-23` |
| `S-06` Review the contract, price, and worker stamina before confirming | `U-09` | `U-20` | `U-18` | `U-18` defines fixed terms and energy profile; `U-20` surfaces them in the final summary/confirm step |

### Section 2 — First Day of Work

| Story | Historical baseline | Primary retrofit unit | Supporting retrofit units | Retrofit role |
|---|---|---|---|---|
| `S-07` Watch the farmhand arrive and work on day one | `U-10`, `U-13B` | `U-21` | — | Runtime now adds visible worker energy and slower pacing |
| `S-08` Execute prioritized work across zones, buildings, and animals | `U-10`, `U-13`, `U-16` | `U-21` | `U-22` | Energy-limited runtime lands in `U-21`; typed-scope animal/greenhouse alignment completes in `U-22` |
| `S-09` Snapshot tool capabilities at spawn and skip what can't be done | `U-10`, `U-13` | `U-21` | `U-24` | Runtime rework must preserve capability snapshot rules; final regression confirms no accidental breakage |
| `S-10` Deposit collected items at shift end | `U-10`, `U-14`, `U-16` | `U-21` | `U-22` | Shift-end behavior loses refund settlement in `U-21`; scope-driven deposit/output routing stays correct in `U-22` |
| `S-11` Receive mail for overflow and unassigned output | `U-14` | `U-22` | `U-24` | Output fallback behavior must remain correct under typed scopes and no-refund semantics; final regression/docs confirm it |

### Section 3 — Daily Life with a Recurring Contract

| Story | Historical baseline | Primary retrofit unit | Supporting retrofit units | Retrofit role |
|---|---|---|---|---|
| `S-12` Pause, cancel, or edit a recurring contract | `U-12`, `U-15` | `U-23` | `U-19`, `U-20` | Persistence and edit-preview groundwork comes first; recurring pricing and day-start behavior completes in `U-23` |
| `S-13` Tune contract prices, worker stamina, and action costs in GMCM | `U-17` | `U-24` | `U-18` | New config shape comes from `U-18`; GMCM exposure and validation finalize in `U-24` |

### Section 4 — Calendar & Edge Cases

| Story | Historical baseline | Primary retrofit unit | Supporting retrofit units | Retrofit role |
|---|---|---|---|---|
| `S-14` Handle festivals, rainy days, and low-work days without confusing contract behavior | `U-15` | `U-23` | `U-18` | Contract-price stability and recurring billing semantics are finalized here |
| `S-15` Player sleeps before the farmhand finishes — shift settles cleanly before rollover | `U-15` | `U-23` | `U-21` | `U-21` removes refund settlement from shift runtime; `U-23` completes calendar/sleep semantics |
| `S-16` Recover from getting stuck | `U-13` | `U-21` | `U-24` | Runtime rework must preserve stuck handling under the new energy/billing model; final regression verifies it |
| `S-17` Survive player attacks without abandoning the shift | `U-13` | `U-21` | `U-24` | Runtime/NPC refresh must not break invulnerability and resume behavior |
| `S-18` Multiplayer refuses to load with a friendly message | `U-08` | `U-24` | — | No major redesign logic changes here; this is a regression/documentation checkpoint story |

### Section 5 — Maintainability

| Story | Historical baseline | Primary retrofit unit | Supporting retrofit units | Retrofit role |
|---|---|---|---|---|
| `S-19` Pure logic separable from SMAPI for testability | `U-02`, `U-04`, `U-05`, `U-06`, `U-10`, `U-17` | `U-18` | `U-19`, `U-21`, `U-24` | New pure seams for contract terms land in `U-18`; persistence and runtime invariants extend in `U-19/U-21`; final regression/docs close the loop in `U-24` |
| `S-20` Externalize all user-visible strings for community translation | `U-08`, `U-17` | `U-24` | `U-20`, `U-23` | New preview/config/calendar strings are introduced earlier, but the dedicated cleanup unit ensures the redesign remains fully i18n-routed |

---

## Coverage verification

| Story | Assigned to at least one retrofit unit? |
|---|---|
| `S-01` | ✅ `U-20`, `U-24` |
| `S-02` | ✅ `U-20`, `U-18` |
| `S-03` | ✅ `U-20`, `U-18`, `U-22` |
| `S-04` | ✅ `U-22`, `U-24` |
| `S-05` | ✅ `U-20`, `U-19`, `U-23` |
| `S-06` | ✅ `U-20`, `U-18` |
| `S-07` | ✅ `U-21` |
| `S-08` | ✅ `U-21`, `U-22` |
| `S-09` | ✅ `U-21`, `U-24` |
| `S-10` | ✅ `U-21`, `U-22` |
| `S-11` | ✅ `U-22`, `U-24` |
| `S-12` | ✅ `U-23`, `U-19`, `U-20` |
| `S-13` | ✅ `U-24`, `U-18` |
| `S-14` | ✅ `U-23`, `U-18` |
| `S-15` | ✅ `U-23`, `U-21` |
| `S-16` | ✅ `U-21`, `U-24` |
| `S-17` | ✅ `U-21`, `U-24` |
| `S-18` | ✅ `U-24` |
| `S-19` | ✅ `U-18`, `U-19`, `U-21`, `U-24` |
| `S-20` | ✅ `U-24`, `U-20`, `U-23` |

**All refreshed stories are assigned to at least one retrofit unit.**

---

## Stories by retrofit unit

| Retrofit unit | Stories touched |
|---|---|
| `U-18` Contract Terms Foundation | `S-02`, `S-03`, `S-06`, `S-13`, `S-14`, `S-19` |
| `U-19` Contract Snapshot Persistence + Legacy Cleanup | `S-05`, `S-12`, `S-19` |
| `U-20` Hiring Flow Preview Refresh | `S-01`, `S-02`, `S-03`, `S-05`, `S-06`, `S-12`, `S-20` |
| `U-21` Worker Energy + Shift Runtime Refresh | `S-07`, `S-08`, `S-09`, `S-10`, `S-15`, `S-16`, `S-17`, `S-19` |
| `U-22` Scope-Driven Runtime Alignment | `S-03`, `S-04`, `S-08`, `S-10`, `S-11` |
| `U-23` Recurring Billing + Calendar Refresh | `S-05`, `S-12`, `S-14`, `S-15`, `S-20` |
| `U-24` Config, Regression, and Documentation Cleanup | `S-01`, `S-04`, `S-09`, `S-11`, `S-13`, `S-16`, `S-17`, `S-18`, `S-19`, `S-20` |

---

## Notes on historically unchanged stories

Some stories are not being reinvented from scratch, but they still need a retrofit unit assignment because the redesign can regress them indirectly.

Examples:
- `S-18` multiplayer guard is historically unchanged, but `U-24` explicitly regression-verifies it after the pricing overhaul
- `S-04` destination assignment UI already existed, but `U-22` must ensure typed scopes do not break how those destinations are consumed at runtime
- `S-17` invulnerability is historically unchanged, but `U-21` touches the worker runtime deeply enough that regression coverage is warranted

---

# SVE Compatibility Units — Story Map (appended 2026-05-29)

Maps the SVE story set (S-21..S-26 from [stories.md](../user-stories/stories.md) Section 6 + S-26) to the SVE units.

| Story | Primary unit | Supporting units | Role |
|---|---|---|---|
| `S-21` Vanilla stays vanilla; SVE support auto-detects | `U-SVE-01` | — | Detection + vanilla-invariance is the foundation's core deliverable |
| `S-22` Farmhand arrives correctly on SVE farm maps | `U-SVE-02` | `U-SVE-01` | Entrance override consumption; seam supplied by the foundation |
| `S-23` Premium Barn/Coop fully serviced | `U-SVE-03` | `U-SVE-01` | Data-driven capacity + premium-tier mapping; policy from the foundation |
| `S-24` New SVE crops/trees/animals/products | `U-SVE-04` | `U-SVE-01` | Classification overrides + graceful skip; seam from the foundation |
| `S-25` Grandpa's Shed is a usable work location | `U-SVE-04` | `U-SVE-01` | Work-location membership + navigation; seam from the foundation |
| `S-26` Add expansion compatibility via one isolated provider | `U-SVE-01` | `U-SVE-02`, `U-SVE-03`, `U-SVE-04` | The provider seam itself; each override unit exercises the extensibility contract |

## Coverage verification

| Story | Assigned to at least one SVE unit? |
|---|---|
| `S-21` | ✅ `U-SVE-01` |
| `S-22` | ✅ `U-SVE-02` |
| `S-23` | ✅ `U-SVE-03` |
| `S-24` | ✅ `U-SVE-04` |
| `S-25` | ✅ `U-SVE-04` |
| `S-26` | ✅ `U-SVE-01` (+ exercised by U-SVE-02/03/04) |

**All SVE stories are assigned to at least one SVE unit.**

## Stories by SVE unit

| SVE unit | Stories touched |
|---|---|
| `U-SVE-01` Provider Foundation + Detection | `S-21`, `S-26` |
| `U-SVE-02` Farm Maps + Worker Entrance | `S-22` |
| `U-SVE-03` Animal Buildings | `S-23` |
| `U-SVE-04` New Content + Grandpa's Shed | `S-24`, `S-25` |
