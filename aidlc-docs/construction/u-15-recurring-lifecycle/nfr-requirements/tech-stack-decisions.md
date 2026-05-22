# U-15 — Tech Stack Decisions

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers

---

## TS-U15-01 — No new frameworks or dependencies
Testing stays on **xUnit** + **FsCheck**. U-15 adds no NuGet package and no manifest dependency: MFM is already required (U-14), and the new logic is plain Mod-layer C# over existing SMAPI events. *(COMPAT-U15-01)*

## TS-U15-02 — CalendarHandlers wraps live game state (NFR-MAINT-03)
`CalendarHandlers` (M-14, `Dayswork/Orchestration/`) is the single place that reads Stardew festival/weather state, exposing `IsFestivalToday()` / `IsRainyToday()` predicates. The festival check and weather flags use Stardew's own day/weather APIs (exact members confirmed at Code Generation, e.g. festival-day utility + current-weather flag). Keeping these behind predicates keeps the scheduler/orchestrator free of direct game lookups and testable. *(BR-CAL, BR-DAY-05)*

## TS-U15-03 — At-save ordering owned by the composition root (FD-Q7=A)
`GameLoop.Saving` wiring moves to: `CalendarHandlers.OnSavingHook` (sleep-stop + settle) **then** `ContractPersistenceAdapter.OnSaving` (persist). `ShiftOrchestrator` stops subscribing to `Saving` directly and instead exposes `StopForSleepAndSettle()`. ModEntry registers the handlers in the required order rather than relying on incidental registration order. *(REL-U15-02, BR-FF-01/02)*

## TS-U15-04 — Mailed refund via MFM money attachment, with text-only fallback (DEV-U15-04, Clar-3=A)
Refund gold is delivered through the existing `MailFrameworkModApiAdapter`. Preferred path: attach money to the settlement letter (combined with any overflow items). If MFM 1.20.0's letter API cannot cleanly carry money (to be confirmed against the installed DLL at Code Generation, as was done for `RegisterLetter` in U-14 / DEV-U14-03), the fallback is a text-only "here's your change" letter whose collection callback credits `Game1.player.Money`. Item-bearing mail continues to use MFM's multi-attachment path. *(REL-U15-04, BR-REF-04)*

## TS-U15-05 — No custom mail/refund save data (NFR-SAFE-03)
Like U-14, U-15 settlement/refund letters are queued **for tomorrow** and persisted by Stardew/MFM — no Dayswork save DTO, no `SaveDataSerializer` change. Playtest corrected the morning skip notices: cannot-afford and festival letters are same-day mailbox entries because they explain why the worker is absent today. Contract status transitions persist through the existing `ContractStore` segment. *(SAFE-U15-03, TS-U14-04 carryover, DEV-U15-06)*

## TS-U15-06 — ToolLevelReader missing-tool semantics change (DEV-U15-03)
`ToolLevelReader.ReadCurrent()` reports a missing tool at the lowest tier instead of "absent/level 0". This is a Mod-layer change to an existing U-10 component; `CapabilityEvaluator` (Core) is unchanged — owned-tool tier gating still applies. The removed tool-missing warning collection/dispatch is deleted from Core/Mod. *(BR-TOOL-01/02/03)*

## TS-U15-07 — Sleep-stop avoids remaining-work mutation (DEV-U15-09)
For v1, sleeping stops the worker instead of running remaining work headlessly. The `Saving` hook flushes only already-performed pending debris sweeps, mails collected-but-undelivered items, mails the refund, and leaves remaining world tasks undone. This keeps sleep settlement bounded and predictable while preserving the ordered save-before-persist contract. *(PERF-U15-03, BR-FF-03)*

## TS-U15-08 — `mailReceived` is a `HashSet<string>` in SV 1.6 (V8)
Any "already sent" mail check (e.g., to avoid duplicate cannot-afford IDs within a save) uses `Game1.player.mailReceived.Contains/Add/Remove` — never indexed access (1.6 changed these from `List` to `HashSet`). Note FD-Q5=A intentionally sends the cannot-afford notice **each** unaffordable day, so per-day uniqueness (not lifetime de-dup) is the relevant concern. *(TS-U14-05 carryover, BR-AFF-03)*

## TS-U15-09 — Shared current deposit-hours policy (DEV-U15-07)
`DepositHoursPolicy` centralizes the current flat 1.0-hour estimate used by both the hire summary and recurring day-start scheduling. This prevents saved zone geometry — especially building placeholder zones `(0,0)..(999,999)` — from inflating recurring deposits until the raw tile-based `HoursEstimator` is redesigned for gameplay pricing.
