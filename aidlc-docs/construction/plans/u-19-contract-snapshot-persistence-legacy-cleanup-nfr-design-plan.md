# U-19 — Contract Snapshot Persistence + Legacy Cleanup: NFR Design Plan

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-19`. See [nfr-requirements/](../u-19-contract-snapshot-persistence-legacy-cleanup/nfr-requirements/).

---

## Plan Checklist

- [x] Analyze NFR requirements artifacts
- [x] Create this NFR design plan
- [x] Evaluate all NFR design question categories and determine whether clarification is needed
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [x] Present completion message and await approval

---

## Pattern Determination

No additional user questions are needed for U-19 NFR Design. The approved NFR requirements and functional design already determine the design patterns cleanly:

- **Resilience patterns** — Applicable and already determined by the approved schema-v2 reliability bar:
  - schema-version gate
  - per-contract exception barrier
  - valid-sibling preservation
  - explicit legacy-drop behavior
- **Scalability patterns** — N/A. This is a local in-process save seam with tiny contract counts; no distributed scale, sharding, or queue patterns apply.
- **Performance patterns** — Applicable and already determined:
  - synchronous inline save/load
  - bounded linear serializer work
  - no async save pipeline or cache subsystem
- **Security patterns** — N/A. Security Baseline is disabled project-wide and the unit has no network/auth/PII surface.
- **Logical components** — Applicable and already determined:
  - `SaveDataSerializer` owns version branching, canonical ordering, and per-entry isolation
  - `ContractStore` owns narrow immutable `ReplaceTermsSnapshot(...)`
  - schema-v2 DTOs carry the authoritative persisted redesign shape
  - dedicated U-19 test-side helpers carry the stronger persistence property bar

The one non-recommended NFR decision (`NFR-Q4=C`) is also explicit enough not to need follow-up: the compatibility bridge remains functionally present, but the design must keep it local and simple because there are no active legacy consumers yet.

---

## Artifact Output

- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/nfr-design/logical-components.md`
