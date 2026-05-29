# Requirements Verification Questions — Stardew Valley Expanded (SVE) Compatibility

**How to use this file**:
- Each question has letter options (A, B, C, …) and an `[Answer]:` line.
- Fill in the letter (or `X` for *Other*, with your custom description on the same line after `[Answer]:`).
- When all answers are filled, reply with "done" (or "completed" / "finished"). I will analyze responses, ask follow-ups if anything is contradictory or ambiguous, then generate `requirements.md`.
- Several questions have a **(Recommended)** option that reflects what the SVE source and the current Dayswork code already support best. If you agree, just put that letter.

> This is a **new active change** on the existing Dayswork project. The previous change (Worker Routing) remains parked at its Build & Test review gate — it is unaffected by this work.

---

## Intent Summary (for confirmation)

Parsed from your request:
- **Request type**: Enhancement / compatibility feature (brownfield — extends the shipping Dayswork mod)
- **Scope estimate**: System-wide — touches worker spawn/entry, animal-building handling, building navigation, world-content classification, and a new mod-detection seam
- **Complexity estimate**: Complex
- **Primary goals**:
  1. Dayswork works correctly when **Stardew Valley Expanded** is installed
  2. **Zero behavior change for vanilla** (no SVE installed)
  3. SVE-specific code is **isolated** from vanilla code
  4. The design is **extensible** to other expansion mods (Ridgeside, East Scarp, etc.) without rework
  5. **Never assume** — every compatibility decision is grounded in SVE source and/or vanilla SDV behavior

### What I verified in the source before writing these questions (so the questions aren't guesses)

**Dayswork's coupling to vanilla content (current code):**
- **Worker spawn/exit** (`ShiftOrchestrator.cs`) is already computed dynamically from `Farm.warps` (first outdoor exit warp), with a hardcoded vanilla fallback tile `(77, 15)`. It is *not* a fixed constant, so it may partly survive SVE maps — but the "first outdoor warp" heuristic can pick the wrong warp on multi-exit SVE maps.
- **Trees & rocks** (`ObjectTargetClassifier.cs`) are classified by SDV *type* (`Tree`/`FruitTree`) and `Object.IsBreakableStone()` — largely content-agnostic, so most new SVE crops/trees/rocks should already work. **However** `ResourceClump` boulders/logs are mapped by *hardcoded vanilla sheet indices*, so any custom SVE clump would be silently skipped.
- **Animal products** (`AnimalTaskHandler.cs`) are collected data-drivenly via `animal.currentProduce` + `ItemRegistry` — content-agnostic, so new SVE animals/products should already collect. **However** `FeedCapacity` hardcodes Deluxe=12 / Big=8 / else=4, `IsAutoFeedBuilding` only matches `"Deluxe"`, and `IsMilkProduce`/`IsShearProduce` string-match `Cow`/`Goat`/`Sheep`.
- **Hiring UI scope** (`Dayswork.Core.Domain.AnimalBuildingTier`) hardcodes exactly the six vanilla tiers (Coop/BigCoop/DeluxeCoop/Barn/BigBarn/DeluxeBarn) — SVE's Premium tiers are **not representable** in the scope selection today.

**SVE source facts (confirmed in `code/Other/Buildings.json`):**
- **Premium Coop** & **Premium Barn** are SDV 1.6 `Data/Buildings` entries with `IndoorMapType: StardewValley.AnimalHouse`, `MaxOccupants: 16`, the standard feed hopper `(BC)99`, and **default-installed AutoPetter `(BC)272` + AutoGrabber `(BC)165`** (meaning pet/collect may already be automated in those buildings).
- SVE ships four map/location packages: **Immersive Farm 2 Remastered** (flagship farm), **Grandpa's Farm**, **Frontier Farm**, and **GrampletonFields** (a separate farmable location reached from Grampleton, not a farm-map replacement).
- **Grandpa's Shed** is an event-gated buildable farm building with its own interior.

---

## Group A — Architecture & Scope

## Question 1 — Isolation & extensibility approach
You want SVE code isolated from vanilla and the design extensible to other expansion mods. How much of that abstraction should this change build now?

A) Define a general **expansion-compatibility provider** seam now (e.g., a vanilla default provider + an SVE provider implementing the same interface for entrance resolution, building/animal capability, and content classification overrides), so adding another expansion later is "write a new provider." More upfront design. **(Recommended — matches goals 3 & 4 directly)**
B) Isolate SVE behavior behind a single SVE-specific module/flag now (clean separation, less abstraction), and extract the general provider interface later when a second expansion is actually added (YAGNI-leaning)
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Which SVE farm maps / locations must this change support?
The worker is farm-centric. SVE's packages differ in kind (farm-map replacements vs. an extra farmable location).

A) **All** of them: Immersive Farm 2 Remastered, Grandpa's Farm, Frontier Farm, **and** GrampletonFields (extra farmable location)
B) The three replacement **farm maps** only — Immersive Farm 2 Remastered, Grandpa's Farm, Frontier Farm — exclude GrampletonFields for now
C) **Immersive Farm 2 Remastered only** (SVE's primary/recommended farm), other maps deferred
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 3 — Worker spawn/entrance on SVE farm maps
You flagged that SVE farm entrances differ greatly from vanilla. Dayswork currently derives the entrance from `Farm.warps` (first outdoor warp) + fallback `(77,15)`. How should the spawn/exit tile be resolved on SVE maps?

A) Keep the dynamic `Farm.warps` detection as the default, and have the SVE provider supply **explicit per-map entrance overrides** only where the warp heuristic picks the wrong tile (grounded in each SVE map's actual warp data) **(Recommended)**
B) For SVE maps, **ignore the heuristic** and always use an explicit per-map entrance coordinate table maintained in the SVE provider
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Group B — SVE Content Handling

## Question 4 — Premium Barn & Premium Coop
Confirmed: these are `AnimalHouse` buildings, `MaxOccupants: 16`, vanilla hopper `(BC)99`, with default AutoPetter + AutoGrabber. Dayswork's `FeedCapacity` would wrongly size them at 4, and the built-in automation may make pet/collect redundant there.

A) Make feeding **data-driven** (read capacity from building data / count actual trough tiles instead of the hardcoded 4/8/12 ladder) **and respect installed AutoPetter/AutoGrabber** (skip pet/collect work the building already automates). This also makes vanilla buildings with player-placed auto-machines behave correctly. **(Recommended)**
B) Treat premium buildings as "Deluxe-equivalent" (capacity ≥12, auto-feed) for sizing, but still always run pet/collect tasks regardless of any auto-petter/auto-grabber present
C) Other (please describe after [Answer]: tag below)

[Answer]: A, but keep in mind the player can move the autopetter and autograbber so don't assume they're always there. I think it would be easiest to just continue to scan the buildings for work, it just won't find anything if the autopetters or autograbbers already did the work. 

## Question 5 — New SVE crops, trees, animals & animal products
Most of these flow through Dayswork's content-agnostic paths (crops via `HoeDirt`/`Crop`, products via `currentProduce`+`ItemRegistry`, trees via `Tree`/`FruitTree` type). Known hardcoded gaps: custom `ResourceClump`s, and the `Cow/Goat/Sheep` string checks for milk/shear.

A) **Rely on the existing data-driven handling** so generic SVE content works automatically; add explicit SVE handling **only** at verified gaps (custom resource clumps, any new milk/wool/other tool-harvest animal types, special tree species) — each gap confirmed against SVE source before coding **(Recommended)**
B) **Exhaustively enumerate and validate** every new SVE crop/tree/animal/product against SVE source and add explicit per-item handling/tests, even where the generic path already works
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6 — Grandpa's Shed
It's a buildable farm building with an interior. (I will inspect its SVE map source to determine exactly what's inside before implementing — this question is about intended scope.)

A) Treat it as a **full work location** — valid for any applicable indoor task its interior actually supports (deposit chests, and indoor crops if present), determined from the SVE map source **(Recommended)**
B) Treat it as a **chest/deposit destination only** (worker may walk in to deposit, but performs no task work inside)
C) **Exclude** Grandpa's Shed from this change
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Group C — Integration & Safety

## Question 7 — How should Dayswork detect SVE and depend on it?
Dayswork must keep running with no SVE installed.

A) **Soft runtime detection** — check for SVE's mod ID via the SMAPI mod registry at startup and activate the SVE provider only when present; **no** hard/optional dependency entry in `manifest.json`; vanilla path is byte-for-byte unchanged when SVE is absent **(Recommended)**
B) Declare SVE as an **optional dependency** in `manifest.json` (load-order hint) and gate the provider on its presence at runtime
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 8 — Behavior when SVE presents something the worker can't handle
For genuinely unsupported content (e.g., an unrecognized custom clump, or a zone tile the worker can't reach on a new map).

A) **Graceful skip**, identical to today's vanilla unknown-object handling — never crash, skip the tile/task, log at debug/trace for maintainers **(Recommended)**
B) Graceful skip **plus** a player-facing "couldn't handle some tasks" mail note at end of shift summarizing what was skipped
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Group D — Extensions (carried forward from project config — confirm or change for this change)

## Question 9 — Security Baseline extension
This change adds no network, PII, auth, or secrets surface (it's local game-content compatibility). The project currently has Security Baseline **disabled**.

A) Keep **disabled** — no security-baseline enforcement for this change (consistent with the rest of the mod) **(Recommended)**
B) Enable — enforce all SECURITY rules as blocking constraints for this change
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 10 — Property-Based Testing extension
The project currently has PBT **enabled in full mode** (FsCheck + xUnit), and the new compatibility logic (entrance resolution, capability/capacity mapping, provider selection) is exactly the kind of pure logic PBT covers well.

A) Keep **enabled in full mode** — PBT rules blocking where applicable, as on prior units **(Recommended)**
B) Partial — PBT only for pure functions / serialization round-trips
C) Disable PBT for this change
X) Other (please describe after [Answer]: tag below)

[Answer]: A
