# Pricing Model Redesign Questions

The discussion notes at `aidlc-docs/inception/requirements/pricing-model-discussion-notes.md` already capture the design direction we agreed on informally. These questions focus only on the remaining decisions needed to turn that direction into formal requirements.

Please answer each question by filling in the letter after the `[Answer]:` tag. If none of the listed options fit, choose `X` and describe your preference after the tag.

## Question 1
How should one-time contracts be priced under the rework?

A) Same pricing model as recurring contracts, just paid once for that day
B) Slight premium over recurring to reward long-term commitment
C) Slight discount over recurring because there is no long-term reservation
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
How should outdoor zone-based pricing be structured?

A) Broad size bands per selected service (for example small / medium / large)
B) Fixed package pricing based on number of zones selected, not area
C) One flat outdoor service price regardless of zone size, relying on energy alone for balance
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
How should rain affect pricing and behavior under the new system?

A) Recurring price stays fixed; rain just means the worker may have less to do that day
B) Recurring price stays fixed, but Water Crops is automatically skipped on rain days
C) Rain can change pricing, but only when the contract is first created or edited
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
How should no-work days be handled for recurring contracts when the worker arrives and finds nothing actionable in the selected scope?

A) Charge the normal recurring price anyway because the labor capacity was reserved
B) Charge a smaller standby fee
C) Skip that day entirely and charge nothing
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
How should festivals affect charging under the redesigned system?

A) Festival day is always skipped with no charge
B) Festival day still charges because the recurring labor slot was reserved
C) One-time contracts skip with no charge, recurring contracts still charge
X) Other (please describe after [Answer]: tag below)

[Answer]: A, continue to send a mail message that day

## Question 6
Which pricing knobs should remain configurable in GMCM after the rework?

A) Only the contract price values
B) Contract price values plus worker energy capacity
C) Contract price values, worker energy capacity, and per-action energy costs
D) Minimal configuration with mostly fixed defaults
X) Other (please describe after [Answer]: tag below)

[Answer]: C

## Question 7
How should unfinished work be communicated to the player when the worker runs out of energy?

A) No special UI; the player can infer it from what was or was not done
B) End-of-day or next-day message summarizing that the worker ran out of energy
C) In-world visible empty energy bar only, with no extra text
X) Other (please describe after [Answer]: tag below)

[Answer]: A
