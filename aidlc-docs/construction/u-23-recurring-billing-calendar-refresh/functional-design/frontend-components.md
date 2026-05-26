# U-23 — Recurring Billing + Calendar Refresh: Frontend Components

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

U-23 is primarily a recurring orchestration unit, but its approved answers do affect the player-facing contract-management surfaces and the wording of same-day notices.

---

## Affected surfaces

| Surface / projection seam | U-23 responsibility |
|---|---|
| Bulletin-board recurring contract entry | Reflect latest known saved recurring price, expose edit/pause/cancel actions, and respect the 6am commitment boundary. |
| Hire/edit review copy | Keep "next eligible day" wording aligned with the approved pre-6am edit behavior. |
| Same-day lifecycle notices | Support only festival skip, cannot afford, and needs-attention messages. |
| Normal rainy / low-work mornings | Stay silent; no new front-end surface is added for those days. |

---

## Bulletin-board recurring management

### Contract summary behavior

The recurring contract summary should continue to show:
- the contract status
- the latest known saved recurring daily price
- the available management actions

Important U-23 interpretation:
- the displayed price is the latest persisted terms snapshot
- the authoritative charge for a future day is still rebuilt again at that day's 6am

So the board remains informative without pretending to be the final future-day billing engine.

### Pre-6am actions

Before 6am, the board should continue to support:
- `Edit`
- `Pause`
- `Cancel`

U-23 requires the UI and wording to be honest about the timing:
- a confirmed edit before 6am affects the next eligible morning immediately
- a pause or cancel before 6am suppresses that morning's run

### After-6am actions

After the recurring day has already committed:
- mid-shift cancel should not present itself as a same-day rollback
- pause/cancel, if exposed at all after 6am, should be framed as affecting future eligible days only

The simplest aligned behavior is the existing one:
- current-day mid-shift cancel is unavailable
- the contract can be managed again once the committed day is no longer in flight

This keeps the UI faithful to the approved no-refund, no-proration recurring model.

---

## Hire/edit review wording alignment

The hire/edit flow does not need another structural redesign in U-23. The important requirement is that copy remains truthful about timing.

The review flow should continue to support language like:
- `This revised fixed daily price applies on the next eligible contract day.`

That wording now maps to the approved U-23 behavior:
- if the edit is confirmed before 6am, the next eligible contract day may be today
- if the edit is confirmed after the current day's commitment boundary, the change applies to the next later eligible morning

No new review page or special calendar widget is required for this unit.

---

## Same-day recurring notices

### Supported notices only

U-23 keeps recurring notice coverage intentionally narrow.

The front-end/text layer should support:
- `Festival skip`
- `Cannot afford`
- `Needs attention`

It should not add:
- rain-explainer notes
- low-work notes
- generic "worker had little to do" notes

### Message intent

The messages should clearly differ in purpose:

| Notice kind | Player-facing intent |
|---|---|
| `Festival skip` | Courtesy explanation that the worker stayed home and no charge was taken. |
| `Cannot afford` | Actionable explanation that today's fixed recurring price could not be covered. |
| `Needs attention` | Actionable explanation that the saved recurring contract can no longer run until the player fixes it. |

### Precedence

If multiple internal reasons compete, the player should only receive one authoritative same-day recurring notice.

The text layer should therefore assume this precedence:
- `Needs attention`
- `Cannot afford`
- `Festival skip`

That prevents a courtesy message from hiding an actionable contract problem.

---

## Non-goals for this unit

U-23 does not require:
- a new calendar screen
- a recurring-history ledger
- a rain-status banner
- a low-work warning badge
- a new mid-shift contract interruption UI

It only needs the existing recurring management and notice surfaces to stay aligned with the approved fixed recurring day-start semantics.
