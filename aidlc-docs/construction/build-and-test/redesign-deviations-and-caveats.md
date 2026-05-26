# Redesign Deviations and Caveats

## Accepted Transitional Deviations

### Legacy financial bridge still exists internally

`DepositAmount` and `HourlyRate` still exist on persisted contracts and transitional compatibility paths. After U-24 they are no longer player-facing config knobs and no longer define the redesign pricing model, but they still remain as internal bridge data until a later persistence cleanup can remove them safely.

### Legacy hourly estimation code still exists for compatibility

The old hourly estimate/rate/deposit code paths are still present for narrow compatibility-only consumers. They are intentionally fenced off from GMCM and the saved redesign-era `config.json` surface.

## Verification Caveats

### Some behavior still requires live-game verification

The following behaviors are covered by targeted automated seams but still need real Stardew playtesting for final confidence:

- bulletin board patch interaction
- live worker pacing feel
- overhead stamina bar readability
- building navigation and outdoor animal chase behavior

### Recurring morning behavior is deterministic but still calendar-sensitive

Automated coverage protects rebuild, affordability, and notice precedence, but same-day notice visibility and festival timing still depend on the real game loop and mailbox behavior.
