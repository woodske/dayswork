# U-20 — Hiring Flow Preview Refresh: Business Rules

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A through FD-Q9=A

These rules govern the refreshed hire/edit flow after the pricing redesign moved contract terms to fixed-price `ContractPreview` / `ContractTermsSnapshot` semantics.

---

## Draft ownership rules

**BR-DRAFT-01 — Typed draft scope is authoritative.** `ContractDraft` stores work scope as `ContractScopeSelection`, not as legacy `Zones`. *(FD-Q1=A)*

**BR-DRAFT-02 — Compatibility `Zones` are confirmation-time output only.** The flow may derive compatibility `Zones` when building the final `Contract`, but they are not the working source of truth during the draft session. *(FD-Q1=A)*

**BR-DRAFT-03 — The flow no longer inserts an implicit whole-farm outdoor zone.** If outdoor services are selected and no outdoor zone exists, outdoor scope is missing and remains missing until the player chooses a real zone. *(FD-Q2=A)*

---

## Preview orchestration rules

**BR-PREVIEW-01 — The coordinator owns preview refresh.** Menus do not compute price or worker-energy details themselves; they consume coordinator-provided preview/view models. *(application design carry-forward)*

**BR-PREVIEW-02 — Task or scope changes recompute core preview.** Changing enabled tasks or typed scope triggers a fresh `ContractTermsBuilder.BuildPreview(...)` call. *(U-18 + FD-Q1=A)*

**BR-PREVIEW-03 — Destination changes do not affect pricing preview.** Output chest/mail/bin assignments do not recompute fixed price or worker energy. *(carry-forward from U-14/U-16 responsibilities)*

**BR-PREVIEW-04 — Schedule changes do not change fixed price.** One-time vs recurring changes Screen 4 payment explanation, but does not change the fixed contract price itself. *(FR-PAY-02, FR-PAY-07, FD-Q8=A)*

---

## Screen 1 task-preview rules

**BR-TASK-01 — Selected services remain visible before scope is complete.** A selected service appears in the Screen 1 preview panel even when the player has not yet selected compatible scope for it. *(FD-Q3=A)*

**BR-TASK-02 — Screen 1 can show explicit “needs scope” states.** Selected services may be marked as needing outdoor scope, animal-building scope, or greenhouse scope instead of silently disappearing from the preview. *(FD-Q3=A)*

**BR-TASK-03 — Missing scope does not trap the player on Screen 1.** The player may continue forward after choosing tasks, even if the preview is not yet confirmable. *(FD-Q3=A, FD-Q9=A)*

---

## Screen 2 scope rules

**BR-SCOPE-01 — Only supported work-scope buildings are selectable in U-20.** Screen 2 offers barns/coops and greenhouse as building scope choices; unsupported buildings are not offered as selectable scope. *(FD-Q4=A)*

**BR-SCOPE-02 — Outdoor zones remain the only outdoor work scope.** Outdoor crop and clearing services require explicit outdoor zones and never inherit a hidden whole-farm fallback. *(FD-Q2=A)*

**BR-SCOPE-03 — Screen 2 summarizes scope by typed family.** Outdoor zones, animal buildings, and greenhouse selection are shown as separate summary sections rather than a generic combined count. *(FD-Q5=A)*

**BR-SCOPE-04 — Animal buildings and greenhouse remain additive scope, not replacements for outdoor zones.** A contract may combine outdoor zones with animal buildings and/or greenhouse selection in the same draft. *(FR-TASK-12 carry-forward)*

---

## Edit-flow rules

**BR-EDIT-01 — Edit flow reopens at the review screen first.** Editing an existing contract opens directly to Screen 4 with current values prefilled and a fresh preview already available. *(FD-Q6=A)*

**BR-EDIT-02 — Back navigation exposes the earlier screens on demand.** From the review screen, the player may navigate backward into schedule, scope/output, and task configuration if changes are needed. *(FD-Q6=A)*

**BR-EDIT-03 — Existing authoritative scope is preferred when hydrating an edit draft.** If the stored contract carries `ScopeSelection`, that authoritative scope seeds the draft directly. *(FD-Q7=A)*

**BR-EDIT-04 — Compatibility zones are a one-time bootstrap only when authoritative scope is absent.** Older contracts lacking `ScopeSelection` may still enter the refreshed edit flow through a best-effort typed-scope derivation from legacy `Zones`. *(FD-Q7=A)*

---

## Summary / confirmation rules

**BR-SUM-01 — Screen 4 is the only confirmation gate.** Earlier screens may remain navigable even when the draft preview is invalid; actual contract confirmation is blocked on Screen 4. *(FD-Q9=A)*

**BR-SUM-02 — Invalid previews disable confirmation and show reasons.** If `ContractPreview.IsValid` is false, Screen 4 must display the validation reasons and disable the confirm action. *(FD-Q9=A)*

**BR-SUM-03 — Screen 4 never mentions hourly billing, deposit estimates, refunds, or hours.** Review/confirm language is fully redesign-era language: fixed price, scope, and worker energy. *(FR-PAY-01, DoD)*

**BR-SUM-04 — Screen 4 shows schedule-sensitive payment timing.** The review screen distinguishes one-time charge-now behavior from recurring next-eligible-day behavior and recurring-edit next-eligible-day behavior. *(FD-Q8=A)*

**BR-SUM-05 — Screen 4 shows worker energy as a plain-language labor cap.** The review screen explains that work continues until completed, the day ends, or the worker’s energy is exhausted. *(FR-HIRE-13, S-06)*

---

## Confirmation outcome rules

**BR-CONFIRM-01 — One-time confirmation stores the confirmed terms snapshot and charges immediately.** A valid one-time contract deducts the fixed price now and persists the exact confirmed `ContractTermsSnapshot`. *(FR-PAY-02, FR-PAY-07)*

**BR-CONFIRM-02 — New recurring confirmation persists scope and terms without same-day one-time charging.** The fixed daily price shown in the draft becomes the recurring daily price of record for the next eligible contract day. *(FR-PAY-07, FD-Q8=A)*

**BR-CONFIRM-03 — Edit confirmation persists revised terms immediately for future use.** Confirming an edited recurring contract saves the new authoritative scope and revised `ContractTermsSnapshot` immediately, ready for the next eligible day. *(U-19 BR-RECUR-01..03 carry-forward)*

**BR-CONFIRM-04 — Confirmed contracts carry authoritative scope plus derived compatibility zones.** The final `Contract` written to the store must include `ScopeSelection`, `TermsSnapshot`, and any derived compatibility `Zones` needed by not-yet-retrofitted runtime consumers. *(FD-Q1=A, U-19 carry-forward)*

---

## Output-assignment rules

**BR-OUT-01 — Output assignment remains orthogonal to preview pricing.** Output destination selection stays on Screen 2, but does not alter price or worker-energy preview. *(carry-forward)*

**BR-OUT-02 — Missing output selections still receive the existing safe default.** Output-producing tasks left without explicit destination assignment continue to fall back to the established default behavior at confirmation time. *(carry-forward)*

---

## Frontend/interaction rules

**BR-UI-01 — The refreshed four-screen flow remains fully gamepad compatible.** The redesign may change preview semantics, but not remove gamepad usability. *(S-01, S-06, S-12 carry-forward)*

**BR-UI-02 — Review-first edit flow must still feel shorter than a fresh hire flow.** The default edit entry point is the review screen, not Screen 1. *(FD-Q6=A, S-12)*

---

## Extension compliance notes

**Security Baseline**: N/A — disabled project-wide.

**Property-Based Testing**: Compliant. U-20 is largely UI orchestration, but the design continues to keep pricing preview and typed-scope rules dependent on pure Core preview seams rather than re-embedding pricing logic into SMAPI menu code.
