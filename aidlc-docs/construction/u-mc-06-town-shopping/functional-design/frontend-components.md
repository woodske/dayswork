# Frontend Components — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Story**: S-30 · **Stage**: Functional Design

U-MC-06 adds **no new authoring screen**. Its player-facing surface is (1) one new **GMCM
config control** for the global preferred store, and (2) the **HUD notice** set the shopping
trip emits. All user-visible strings are routed through i18n (`i18n/default.json`,
NFR-MC-07).

---

## 1. GMCM — global preferred store (DEV-MC-06-01)

A single dropdown added to the existing GMCM config (registered by `GMCMRegistrar`),
grouped under the Manage Crops / Worker Behavior section.

| Property | Value |
|----------|-------|
| Control | Dropdown (text-choice) |
| Key | `ModConfig.PreferredCropStore` |
| Allowed values | `Either`, `Pierre`, `Joja` |
| Default | `Either` |
| Label (i18n) | `config.preferred_crop_store.name` |
| Tooltip (i18n) | `config.preferred_crop_store.tooltip` — "Which store the farmhand buys seeds and fertilizer from. Anything the preferred store doesn't stock is bought at the other store. 'Either' buys wherever it's available." |

**Behaviour bound to this control**
- Applies to **all** crop groups uniformly (overrides any per-zone preference).
- Drives `StoreResolver` preferred-then-other resolution (BR-MC6-01/02).
- `Pierre` on a Wednesday → automatic fallback to Joja with a fallback HUD notice (the
  control is not date-aware; fallback is runtime).
- No live value validation needed beyond the three enum choices; reused immediately on the
  next shift (no restart).

---

## 2. HUD notices (M-29 `CropHudNotifier`)

In-game HUD messages emitted by the shopping trip. Each is deduped where noted to avoid spam,
and every string is i18n-keyed.

| Notice | When | i18n key (indicative) | Dedupe |
|--------|------|------------------------|--------|
| **Purchase completed** | one per purchase **beat**, as it completes (FR-MC-19) | `notify.bought_item` ("Bought {{count}}× {{item}}") | per line (one per beat) |
| **Using fallback store** | preferred store closed → bought at the other (FR-MC-20) | `notify.using_fallback_store` | once per shift |
| **Festival skipped** | festival day, purchasing skipped (FR-MC-16) | `notify.shopping_festival_skipped` | once per shift |
| **Insufficient funds** | wallet < manifest cost; bought max affordable (FR-MC-12) | `notify.shopping_insufficient_funds` | once per shift |
| **Shopping unavailable** | route/bind failure → skipped, planting from chest (DEV-MC-06-02) | `notify.shopping_unavailable` | once per shift |

**Pacing.** Purchase-completed notices fire at the `WorkerActionAnimationMs` cadence (one per
beat), so the player sees the trip resolve item-by-item at the worker's normal rhythm rather
than all at once (FR-MC-19). No notice fires for chest-supply-only crops (BR-MC6-04).

---

## 3. Interaction flow (player's view)

```
Player sets "Preferred crop store" in GMCM (once).
        │
        ▼
Shift runs. If the farmhand needs seeds/fertilizer it can't find in the input chest:
        │
        ├─ (festival)            → "Shopping skipped — festival today."
        ├─ (nothing purchasable) → no message; plants what the chest allows
        ├─ (route/shop failure)  → "Couldn't reach the store — planting with what's on hand."
        └─ (normal)              → walks to town; player sees, in rhythm:
                                     "Bought 12× Parsnip Seeds"
                                     "Bought 12× Basic Fertilizer"
                                     [if Pierre closed] "Pierre's is closed — shopped at JojaMart."
                                     [if short on gold] "Not enough gold — bought what was affordable."
                                   …then walks back and plants. Leftovers go to the input chest.
```

**Input methods.** The GMCM dropdown follows GMCM's own gamepad + mouse/keyboard handling
(no custom input code). HUD notices are display-only (`Game1.addHUDMessage`), no input.
