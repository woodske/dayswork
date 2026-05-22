# U-15 — Tech Stack Decisions

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers

---

## TS-U15-01 — No new frameworks or dependencies
Testing stays on **xUnit** + **FsCheck**. U-15 adds no NuGet package and no manifest dependency: MFM is already required (U-14), and the new logic is plain Mod-layer C# over existing SMAPI events. *(COMPAT-U15-01)*

## TS-U15-02 — CalendarHandlers wraps live game state (NFR-MAINT-03)
`CalendarHandlers` (M-14, `Dayswork/Orchestration/`) is the single place that reads Stardew festival/weather state, exposing `IsFestivalToday()` / `IsRainyToday()` predicates. The festival check and weather flags use Stardew's own day/weather APIs (exact members confirmed at Code Generation, e.g. festival-day utility + current-weather flag). Keeping these behind predicates keeps the scheduler/orchestrator free of direct game lookups and testable. *(BR-CAL, BR-DAY-05)*

## TS-U15-03 — At-save ordering owned by the composition root (FD-Q7=A)
`GameLoop.Saving` wiring moves to: `CalendarHandlers.OnSavingHook` (fast-forward + settle) **then** `ContractPersistenceAdapter.OnSaving` (persist). `ShiftOrchestrator` stops subscribing to `Saving` directly and instead exposes `FastForwardAndSettle()`. ModEntry registers the handlers in the required order rather than relying on incidental registration order. *(REL-U15-02, BR-FF-01/02)*

## TS-U15-04 — Mailed refund via MFM money attachment, with text-only fallback (DEV-U15-04, Clar-3=A)
Refund gold is delivered through the existing `MailFrameworkModApiAdapter`. Preferred path: attach money to the settlement letter (combined with any overflow items). If MFM 1.20.0's letter API cannot cleanly carry money (to be confirmed against the installed DLL at Code Generation, as was done for `RegisterLetter` in U-14 / DEV-U14-03), the fallback is a text-only "here's your change" letter whose collection callback credits `Game1.player.Money`. Item-bearing mail continues to use MFM's multi-attachment path. *(REL-U15-04, BR-REF-04)*

## TS-U15-05 — No custom mail/refund save data (NFR-SAFE-03)
Like U-14, all U-15 letters (settlement/refund, cannot-afford, festival) are queued **for tomorrow** and persisted by Stardew/MFM — no Dayswork save DTO, no `SaveDataSerializer` change. Contract status transitions persist through the existing `ContractStore` segment. *(SAFE-U15-03, TS-U14-04 carryover)*

## TS-U15-06 — ToolLevelReader missing-tool semantics change (DEV-U15-03)
`ToolLevelReader.ReadCurrent()` reports a missing tool at the lowest tier instead of "absent/level 0". This is a Mod-layer change to an existing U-10 component; `CapabilityEvaluator` (Core) is unchanged — owned-tool tier gating still applies. The removed tool-missing warning collection/dispatch is deleted from Core/Mod. *(BR-TOOL-01/02/03)*

## TS-U15-07 — Headless fast-forward reuses the live task pipeline (FD-Q2=A)
The fast-forward does not re-implement task logic: it drives the **same** task-detection + invocation handlers the live shift uses, minus walking/animation, charging estimated per-action in-game-minutes against the remaining window. Whether to cap per-frame mutation volume for very large zones is an engineering detail **resolved in NFR Design / Code Generation**; the product rule is atomic settlement before day-rollover. *(PERF-U15-03, BR-FF-03)*

## TS-U15-08 — `mailReceived` is a `HashSet<string>` in SV 1.6 (V8)
Any "already sent" mail check (e.g., to avoid duplicate cannot-afford IDs within a save) uses `Game1.player.mailReceived.Contains/Add/Remove` — never indexed access (1.6 changed these from `List` to `HashSet`). Note FD-Q5=A intentionally sends the cannot-afford notice **each** unaffordable day, so per-day uniqueness (not lifetime de-dup) is the relevant concern. *(TS-U14-05 carryover, BR-AFF-03)*
