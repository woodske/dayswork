# Worker Player Action Isolation Code Generation Plan

## Unit Context
- **Unit**: `worker-player-action-isolation`
- **Request type**: Focused runtime bug fix for player/farmhand autonomy during active worker task beats
- **Scope**: Existing `Dayswork` orchestration snapshot guard and `Dayswork.Tests` snapshot coverage
- **Public interfaces**: No SMAPI manifest, config, save-data, or player-facing API changes
- **Extension configuration**: Security Baseline disabled; Property-Based Testing applicable to the pure snapshot restore invariant

## Execution Steps
- [x] Step 1: Record the raw implementation request in `aidlc-docs/audit.md`, create this focused code-generation plan, and update `aidlc-docs/aidlc-state.md` with the active review fix.
- [x] Step 2: Replace reset-style player animation restoration with progress-preserving restore state in `WorkerActionPlayerStateSnapshot`.
- [x] Step 3: Add low-noise diagnostic logging around guarded worker task actions when vanilla callbacks mutate real-player action state.
- [x] Step 4: Expand snapshot example tests for active animation progress, idle cleanup, and transient action/movement flags.
- [x] Step 5: Add FsCheck property coverage for the pure snapshot capture/mutate/restore invariant.
- [x] Step 6: Run compile-only build and test verification with mod deployment disabled.
- [x] Step 7: Run deploy-enabled build when the Stardew Valley mod folder is available.
- [x] Step 8: Update this plan, code summary, state, and audit entries with final results.

## Content Validation
- Markdown lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
