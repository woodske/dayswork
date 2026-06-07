# NFR Design Plan — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Stage**: CONSTRUCTION — NFR Design

## Approach
No blocking question round. All five mandatory NFR Design categories were evaluated against
the approved Functional Design and NFR Requirements; the pattern set is fixed by the existing
pure-Core/thin-adapter architecture plus the two user decisions (DEV-MC-06-01/02). The
approval gate is retained (not auto-continued).

## Category evaluation
- **Resilience** — skip-on-failure barrier for town navigation + headless bind (DEV-MC-06-02);
  store-hours deferral/fallback; festival/insufficient-funds/all-closed degradations.
  *Pattern decided; no user input needed.*
- **Scalability** — N/A in the cloud sense; bounded to one trip / two stores / O(zones×items)
  manifest per shift. *No infrastructure scaling.*
- **Performance** — read live shop snapshot once per store per shift (cache); single up-front
  manifest; reuse bounded routes + existing beat cadence. *Pattern decided.*
- **Security** — N/A (local in-game gold deduction; no network/PII/auth). Security Baseline disabled.
- **Logical components** — pure Core seams (manifest, affordability, store-hours, planned-tile
  count) + live adapters (shop reader, purchase service, town routes) + a gold-safety barrier.

## Checklist
- [x] Analyze NFR Requirements (NFR-MC6-01..10; gold-safety + resilience blocking).
- [x] Decide pattern set (P1..P10 below) consistent with pure-Core/thin-adapter architecture.
- [x] Generate `nfr-design-patterns.md`.
- [x] Generate `logical-components.md`.
- [x] Present standardized 2-option completion message; wait for explicit approval.
