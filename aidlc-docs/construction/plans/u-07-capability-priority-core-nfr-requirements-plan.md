# U-07 Capability & Priority Core — NFR Requirements Plan

## Execution Checklist

- [x] Step 1: Analyze functional design artifacts
- [x] Step 2: Create NFR requirements plan (this file)
- [x] Step 3: Assess questions needed — none required (all NFRs determinable from prior decisions + unit scope)
- [x] Step 4: Store plan (this file)
- [x] Step 5: N/A (no questions asked)
- [x] Step 6: Generate NFR requirements artifacts
- [x] Step 7: Present completion message
- [ ] Step 8: Wait for explicit approval
- [ ] Step 9: Record approval and update progress

## Assessment Notes

U-07 introduces only pure stateless Core types (ToolLevel, ToolSnapshot, AxeTarget,
PickTarget, CapabilityMatrix, CapabilityEvaluator, TaskPriorityOrderer). All code
lands in `Dayswork.Core/` — the no-SMAPI project. The NFR picture is identical in
structure to U-04/U-05/U-06:

- NFR-MAINT-03 (Core isolation): BLOCKING — same enforcement as all prior Core units
- PBT-03 (invariant properties): ENFORCED — TaskPriorityOrderer determinism property
- PBT-07 (shared generators): ENFORCED — ToolSnapshotGen for downstream use (U-10+)
- All other NFRs: N/A or advisory (no serialization, no UI, no gold math, no persistence)

No new tech stack decisions needed.
