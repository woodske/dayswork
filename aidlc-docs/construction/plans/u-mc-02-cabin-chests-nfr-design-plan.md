# NFR Design Plan - U-MC-02 Cabin Chests

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete

## Plan Checklist

- [x] Load NFR Design rule details.
- [x] Load U-MC-02 NFR Requirements artifacts.
- [x] Apply user instruction to continue with recommended NFR choices.
- [x] Evaluate resilience, scalability, performance, security, and logical component categories.
- [x] Determine no separate NFR Design question file is needed because recommended choices were explicitly authorized.
- [x] Generate NFR Design artifacts.
- [x] Update workflow state and audit.

## Recommended Design Patterns Applied

| Category | Pattern |
|---|---|
| Resilience | Idempotent ensure; narrow skip on missing chest/office. |
| Performance | Lifecycle-only backfill; direct tile/ID checks. |
| Security | No additional pattern; security baseline disabled and no security surface. |
| Maintainability | Role service encapsulation in `CabinChestService`; centralized constants on `HiringBuilding`. |
| Usability | Fixed i18n labels and explicit chest roles. |
