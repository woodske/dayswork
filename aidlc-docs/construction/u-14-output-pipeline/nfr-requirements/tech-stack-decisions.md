# U-14 — Tech Stack Decisions

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail

---

## TS-U14-01 — No new test/runtime frameworks
Testing stays on **xUnit** + **FsCheck** (NFR-MAINT-01/02). `DepositPlanner` is plain Core C#. The only new external integration is the Mail Framework Mod (MFM) **API**, not a NuGet package — integration is through a runtime adapter (see TS-U14-03).

## TS-U14-02 — Distance oracle injected into the pure planner (NFR-MAINT-03)
`DepositPlanner` takes a `Func<TileCoord,TileCoord,int>` distance oracle rather than referencing any Stardew pathfinding type. v1 supplies **Manhattan distance**, consistent with U-13's existing nearest-task work routing (DEV-02). This keeps the planner pure Core and PBT-testable, and lets a future version swap in true path-distance without changing the planner. *(BR-OUT-04)*

## TS-U14-03 — MFM integration: runtime adapter over installed API (V9)
Code generation initially tried the GMCM-style copied interface pattern, then corrected it after inspecting the installed MFM 1.20.0 DLL. MFM exposes `RegisterLetter(ILetter, Func<ILetter,bool>, Action<ILetter>, Func<ILetter,List<Item>>)`, so Dayswork now acquires the raw API object with `Helper.ModRegistry.GetApi("DIGUS.MailFrameworkMod")` on `GameLoop.GameLaunched` and uses `MailFrameworkModApiAdapter` to create MFM `Letter`/`ApiLetter` objects by reflection. Item-bearing overflow mail and no-item warning mail both use MFM letters. *(BR-DEP-01, BR-MAIL-01/05, DEV-U14-03)*

## TS-U14-04 — No custom mail save data (FD-Q4=A)
Overflow and warning letters are queued **for tomorrow** at shift end and persisted by Stardew/MFM, not by Dayswork. No new save DTO, no `ContractStore`-style structure, no SaveDataSerializer change. This is a deliberate simplification that removes a whole NFR-SAFE-03 round-trip surface. *(SAFE-U14-03)*

## TS-U14-05 — `mailReceived` is a `HashSet<string>` in SV 1.6 (V8)
Any "already sent / already received" mail check uses `Game1.player.mailReceived.Contains(mailId)` and `.Add` / `.Remove` — never indexed access (1.6 changed these from `List` to `HashSet`). Relevant if the dispatcher dedupes warning mail IDs. *(design-verification-notes V8)*

## TS-U14-06 — ItemBuffer extension carries `SourceTask` (FD-Q1=A)
`C-10 ItemBuffer` stays pure Core but its records gain `SourceTask : TaskKind` (and `QualifiedItemId` per V6). This is a Core-only signature change (`Add(itemId, qty, sourceTask)`; `Snapshot`/`TakeAll` return `BufferedItem`). Recorded as a deviation from the component matrix (which listed C-10 as not-extended). *(domain-entities.md)*

## TS-U14-07 — MFM large-attachment & acquisition-failure handling — DEFERRED to NFR Design
Two internal pattern choices are recorded here and **resolved in NFR Design**, as they are engineering details, not product preferences:
- **Large attachment set:** whether to hand MFM the full overflow set in one call (preferred — preserves S-11 "one letter") and how to behave if MFM has a practical per-letter attachment cap. Product rule stays "one letter, all items"; the mechanism is the open part. *(REL-U14-04)*
- **`GetApi` returns null at runtime:** log-and-continue without crashing, keeping item-safety intact. *(REL-U14-05)*
