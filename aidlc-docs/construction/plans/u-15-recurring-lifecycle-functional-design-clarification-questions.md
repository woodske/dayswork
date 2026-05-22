# U-15 Functional Design — Clarification Questions

Three of your FD answers either reverse a previously-approved requirement or have a Stardew mechanic wrinkle worth confirming before I generate the design artifacts. Please answer each `[Answer]:` tag below.

---

## Clarification 1 — Festival days (re: FD-Q3 = C "don't skip festival days")

Your answer means the worker works **normally on festival days** with no skip. Two things to confirm, because this reverses FR-DAY-01 and the S-14 "festival skip" clause:

- **Requirements impact**: FR-DAY-01 ("on festival days the worker does not show up; recurring deposit not deducted; no mail") would be **deviated**. On a festival day a recurring contract would now charge its daily deposit and run a shift like any other day.
- **Stardew mechanic wrinkle**: a festival **freezes in-game time** and **warps the player to the festival map**. The worker's shift only advances while game time is flowing — so in practice the worker works in the **morning before the player leaves for the festival**, then makes no further progress during the festival event itself. Whatever isn't finished is settled the normal way (refund / sleep fast-forward).

### Clarification 1a
How should U-15 treat festival days?

A) **No festival handling at all** — a festival day is just a normal day: recurring deposit charged, shift runs, refund/sleep-settle as usual; accept that the worker only progresses while time flows (typically the pre-festival morning). Drop `IsFestivalToday()` and all festival-skip logic from U-15 scope. *(matches your FD-Q3 = C)*
B) **Skip festival days after all** — revert to FD-Q3 = A (worker doesn't show, recurring deposit not charged, one-time deposit refunded). Keeps FR-DAY-01 intact.
C) Other (please describe after [Answer]: tag below)

[Answer]: C, skip festival days, but send a letter

---

## Clarification 2 — Missing tools (re: FD-Q8 = C "defaults to lowest tier")

Today FR-TOOL-03 + U-13 treat a tool the player doesn't own as **level 0**: the worker **skips** every task needing it and a **tool-missing warning mail** is sent next morning. Your answer changes that to: a missing tool **defaults to the lowest-tier (basic/starter) tool**, so the worker just does the task — no skip, no warning. Confirming the exact semantics and knock-on effects:

### Clarification 2a — Substitution semantics
A) If a required tool is missing from the player, the worker uses the **lowest-tier version** of that tool and performs the task normally; no skip, no warning mail. *(matches your FD-Q8 = C)*
B) Other (please describe after [Answer]: tag below)

[Answer]: A

### Clarification 2b — Tier gating for tools the player DOES own
The phrase "lowest tier" implies tool *tiers* still matter. For tools the player actually owns, should capability still respect their real tier?

A) **Yes** — keep U-13's capability rules for owned tools (e.g., a player who owns only a basic pickaxe still can't break boulders/meteorites that need a higher tier; fruit trees still always-skip per FR-SKIP-03). **Only the missing-tool branch changes** (missing → basic tier instead of "absent → skip"). *(Recommended — smallest change, keeps U-13 logic)*
B) **No** — ignore tool tiers entirely; the worker performs every task at full capability regardless of the player's tools.
C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Clarification 2c — The now-unreachable tool-missing warning path
With missing tools no longer causing skips, the tool-missing warning machinery built in U-13 (`ShiftContext.ToolMissingWarnings`) + U-14 (`MailDispatcher.QueueToolMissingWarning`) can never fire. This also makes FD-Q8 itself moot. What should U-15 do with it?

A) **Remove it** — delete the `ToolMissingWarnings` collection + `QueueToolMissingWarning` dispatch since they're now dead code. *(Recommended — no dead code; FR-TOOL-03's warning clause is formally dropped)*
B) **Leave it in place but inert** (never populated) in case missing-tool skipping returns later.
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Clarification 3 — Mailed-refund money attachment (re: FD-Q9 = C, sub-note left blank)

You chose to mail gold-bearing refunds. The sub-note asks about the fallback if Mail Framework Mod can't cleanly attach **money** to a letter (vanilla mail supports it natively; MFM's money support I'll confirm at code time).

A) **Fallback acceptable** — if MFM can't attach money, send a text-only "here's your change" letter that credits the gold when the letter is *collected* (still next morning, still immersive). *(Recommended)*
B) **No fallback** — if MFM can't attach money, keep direct gold credit at exit instead (abandon mailed refund).
C) Other (please describe after [Answer]: tag below)

[Answer]: A
