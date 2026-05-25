# U-20 — NFR Requirements

**Unit**: U-20 — Hiring Flow Preview Refresh

U-20 is a player-facing hire/edit flow retrofit unit. Its NFR surface is centered on **immediate synchronous preview refresh**, **strict deterministic preview and review output**, **best-effort resilience for legacy edit hydration**, **preserved gamepad-friendly interaction quality**, and **strong regression coverage that keeps pricing logic out of the menus**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q9=A apply throughout.

---

## Performance

### PERF-U20-01 — Preview refresh remains immediate and synchronous (NFR-Q1=A)
Task and scope changes must refresh pricing and worker-energy preview inline during normal menu interaction, with no debounce, background worker, or visible lag in typical use.

This unit is not permitted to require:
- async preview orchestration
- deferred review-model recomputation
- background caching or chunked preview updates

### PERF-U20-02 — Non-pricing edits stay lightweight
Changes to:
- output destination assignments
- schedule selection

must not trigger unnecessary price recomputation work. Destination edits remain orthogonal to price/energy preview, and schedule edits may refresh only schedule-sensitive review copy.

### PERF-U20-03 — No speculative UI-performance complexity over a tiny data set
U-20 should not introduce caches, throttling layers, or specialized memoization for a flow that operates over very small draft data. The intended performance strategy is small synchronous recomputation plus clear separation of preview-producing and copy-only mutations.

---

## Reliability & Correctness

### REL-U20-01 — Equivalent drafts produce strictly deterministic preview output (NFR-Q2=A)
Equivalent combinations of:
- enabled tasks
- typed scope selection
- relevant config snapshot
- schedule context

must produce the same:
- service contribution rows
- validation reasons
- pricing totals and breakdown structure
- worker energy summary structure
- summary/review ordering

across runs and machines.

### REL-U20-02 — Determinism must not depend on incidental collection order
Preview/view-model output must not rely on raw enumeration order from sets, dictionaries, or screen-local mutation history. Canonical ordering must be enforced before user-facing rows and summaries are emitted.

### REL-U20-03 — Invalid preview handling is explicit and stable
If the draft is invalid, the flow must:
- keep the preview reachable
- show the validation reasons
- disable confirm on Screen 4 only

It must not silently invent missing scope, hide selected services, or reintroduce whole-farm fallback behavior.

### REL-U20-04 — Legacy edit hydration failures degrade safely
When older contracts need compatibility-zone bootstrap, missing or imperfect legacy data must degrade into an explicit editable/invalid state rather than a crash, hidden auto-fill, or corrupted review model.

---

## Safety & Data Integrity

### SAFE-U20-01 — Redesign-era financial language must stay authoritative in the UI
The refreshed hire/edit flow must not reintroduce hourly-rate, estimated-hours, deposit, or refund semantics on Screen 1 through Screen 4. U-20's user-facing financial story is fixed price, typed scope, and worker energy.

### SAFE-U20-02 — Invalid drafts cannot be confirmed
`ContractPreview.IsValid = false` must always prevent confirmation, even if the player can still navigate the earlier screens.

### SAFE-U20-03 — No silent scope invention during edit hydration
Legacy bootstrap may derive typed scope from compatibility `Zones`, but U-20 must not fabricate scope beyond what the bootstrap can justify. Missing real scope remains missing and should be surfaced honestly.

---

## Usability & Interaction Quality

### USAB-U20-01 — Selected services remain legible before scope is complete
The player must still see selected services on Screen 1 even when they are not yet chargeable, along with clear “needs scope” style explanations rather than silent disappearance.

### USAB-U20-02 — Review-first edit flow remains meaningfully shorter than a new hire flow (NFR-Q4=A)
Editing an existing contract must reopen at the review screen first and preserve straightforward back-navigation, so routine edits feel shorter and lighter than starting a new contract from Screen 1.

### USAB-U20-03 — Gamepad compatibility is preserved (NFR-Q4=A)
The redesign may change preview semantics, but it must not require mouse-only interaction patterns or remove existing gamepad-friendly navigation expectations.

### USAB-U20-04 — Recovery path for invalid preview is obvious
When confirm is disabled, the player must be able to tell:
- why the draft is invalid
- which kind of scope is missing
- that back-navigation is the way to fix it

The unit should avoid dead-end or confusing “continue but nothing works” behavior.

---

## Maintainability & Testability

### MAINT-U20-01 — Pricing and worker-energy logic remain outside the menus
U-20 menus remain presentation-focused. They consume coordinator-provided preview/view models rather than recomputing pricing, scope interpretation, or energy semantics locally.

### MAINT-U20-02 — Strong example + property coverage is required (NFR-Q5=A)
Because U-20 is the visible switchover to the redesign model, it carries a stronger regression bar than a purely cosmetic UI refresh. It requires:
- focused example-based coordinator/view-model tests
- meaningful FsCheck coverage where pure invariants exist
- explicit legacy edit hydration regression coverage

### MAINT-U20-03 — Property coverage must target orchestration invariants
At minimum, the FsCheck-friendly portion of U-20 must exercise:
- equivalent-draft determinism
- no-whole-farm-fallback behavior
- schedule-change-does-not-change-price invariants
- destination-change-does-not-change-price invariants
- compatibility-zone bootstrap outcomes where pure helpers exist

### MAINT-U20-04 — No new UI framework or automation stack is required
U-20 should stay on the existing SMAPI menu/coordinator architecture. The quality bar is achieved by better state ownership and regression tests, not by introducing a new UI toolkit or end-to-end UI automation framework.

---

## Compatibility / Retrofit Support

### COMPAT-U20-01 — Legacy contracts remain editable on a best-effort basis (NFR-Q3=A)
Contracts that lack authoritative `ScopeSelection` but still carry compatibility `Zones` should still enter the refreshed edit flow whenever typed-scope bootstrap is possible.

### COMPAT-U20-02 — Authoritative scope stays preferred when available
If a stored contract already carries authoritative `ScopeSelection`, that data must seed the edit draft directly rather than being re-derived from compatibility data.

### COMPAT-U20-03 — Compatibility-era runtime fields may persist, but they are not the U-20 UI source of truth
Downstream runtime compatibility fields can continue to exist until later retrofit units finish cutover, but the hire/edit flow must read and present redesign-era authoritative scope and preview data.

---

## Availability / Security / Infrastructure

### AVAIL-U20-01 — No availability-specific requirements
U-20 is an in-process single-player UI/orchestration seam. It has no external uptime, failover, or disaster-recovery surface.

### SEC-U20-01 — Security Baseline is N/A
Security Baseline is disabled project-wide (`NFR-SEC-01`). U-20 has no network, auth, or PII surface. Security Baseline rules are N/A for this unit.

### INFRA-U20-01 — No infrastructure decisions introduced
U-20 requires no service deployment, queue, external datastore, or infrastructure mapping beyond the existing `.NET 6` / SMAPI mod runtime.

---

## Property-Based Testing Obligations

### PBT-U20-01 — Equivalent-draft determinism invariants
Equivalent drafts and equivalent typed scope expressed in different selection orderings must produce the same preview totals, the same contribution semantics, and the same canonical review-model ordering.

### PBT-U20-02 — No-whole-farm-fallback invariants
Outdoor task selections without explicit outdoor zones must remain unscoped/invalid rather than silently becoming chargeable through an implied farm-wide scope.

### PBT-U20-03 — Schedule/destination non-pricing invariants
Changing schedule or output destination assignments must not change fixed price or worker-energy preview content.

### PBT-U20-04 — Legacy bootstrap safety invariants
Where a pure bootstrap helper exists, compatible legacy-zone inputs must either:
- derive a supported typed-scope result, or
- degrade into an explicit incomplete state

without fabricating unsupported scope.
