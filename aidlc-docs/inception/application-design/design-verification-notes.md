# Design Verification Notes — Cross-checked against current Stardew/SMAPI docs

**Generated**: 2026-05-18 after live fetches of the Stardew Valley wiki and related sources.

**Why this exists**: The Application Design artifacts ([application-design.md](application-design.md), [components.md](components.md), [component-methods.md](component-methods.md), [services.md](services.md), [component-dependency.md](component-dependency.md)) were initially produced from training-data knowledge of SMAPI/Stardew APIs. This document captures the result of checking that knowledge against the current docs.

**Sources fetched**:
- [Modding:Modder_Guide/Get_Started](https://wiki.stardewvalley.net/Modding:Modder_Guide/Get_Started) — SDK / platform versions
- [Modding:Modder_Guide/APIs/Events](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Events) — SMAPI events list
- [Modding:Modder_Guide/APIs](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs) — IModHelper surface
- [Modding:Modder_Guide/APIs/Harmony](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Harmony) — patching guidance
- [Modding:Modder_Guide/APIs/Translation](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Translation) — i18n
- [Modding:Modder_Guide/APIs/Data](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Data) — per-save data
- [Modding:Modder_Guide/APIs/Multiplayer](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Multiplayer) — multiplayer API
- [Modding:Modder_Guide/APIs/Utilities](https://wiki.stardewvalley.net/Modding:Modder_Guide/APIs/Utilities) — Context properties
- [Modding:Common_tasks](https://stardewvalleywiki.com/Modding:Common_tasks) — mail / mailbox recipes
- [Modding:Migrate_to_Stardew_Valley_1.6](https://stardewvalleywiki.com/Modding:Migrate_to_Stardew_Valley_1.6) — 1.6 breaking changes
- Search-derived info for `PathFindController`, `IClickableMenu` overrides, GMCM API

---

## Part 1 — Design decisions that survived verification

| Decision in our design | Confirmed by |
|---|---|
| Target **SMAPI 4.0+ on .NET 6** | Get_Started page explicitly states: "You need .NET 6 because it's the version used by the game." Minimum SMAPI API version 4.0.0. |
| **xUnit** as test framework | Get_Started lists Visual Studio Community / Rider / VS Code as supported IDEs; no test-framework preference imposed — xUnit is the standard .NET 6 choice. |
| **SMAPI event names** used in services.md (`UpdateTicked`, `TimeChanged`, `SaveLoaded`, `Saving`, `DayStarted`, `DayEnding`) | All present in the Events API page verbatim. |
| **`Display.RenderedWorld`** for the zone-draw overlay | Present in Display events list. |
| **`Helper.Data.ReadSaveData<T>` / `WriteSaveData<T>`** for `ContractPersistenceAdapter` | Wiki confirms exact method names and notes data is auto-scoped to your mod (no need to prefix with UniqueID). |
| **`helper.Translation.Get(key, tokens)`** with fluent chain (`.Tokens(...).Default(...)`) | Wiki confirms exact pattern including double-brace placeholders `{{name}}` and locale fallback (`pt-BR.json → pt.json → default.json`). |
| **`i18n/default.json`** layout for our `I18nHelper` | Wiki confirms folder layout exactly. |
| **Hand-wired composition root** in `ModEntry.Entry()` (no DI container) | Wiki examples use this pattern throughout. |
| **Harmony postfix preferred over prefix** for BulletinBoardPatch | Wiki: "use postfixes when possible for best compatibility and stability." |
| **Harmony patch methods must be `static`** | Wiki confirms. |
| **Per-save data restricted to "save loaded"** state | Wiki: "the save file must be loaded (e.g., it won't work on the title screen)." Our `ContractPersistenceAdapter` already subscribes to `SaveLoaded` / `Saving`, which is the right window. |
| **`Context.IsMultiplayer`** exists (for `MultiplayerGuard`) | Wiki Utilities page confirms `Context.IsMultiplayer` (bool), `Context.IsSplitScreen` (bool), `Context.IsMainPlayer` (bool — **true in single-player**). |

---

## Part 2 — Adjustments to apply before Construction

These are findings that change how Construction code should be written. Most are small. None invalidate the component layout decided in [application-design.md](application-design.md).

### V1 — Csproj must include `<EnableHarmony>true</EnableHarmony>`

**What we missed**: The design says "Harmony is used for BulletinBoardPatch" but doesn't specify the csproj wiring. Per the wiki, Harmony ships with SMAPI but is **opt-in** per-mod via a build flag.

**Action in Construction**: The `Dayswork.csproj` (Code Generation Step 1) MUST include:
```xml
<PropertyGroup>
  <EnableHarmony>true</EnableHarmony>
</PropertyGroup>
```
No additional NuGet package — Harmony comes from the ModBuildConfig bundle.

---

### V2 — `Pathoschild.Stardew.ModBuildConfig` NuGet is required

**What we missed**: The design didn't enumerate build-time NuGet dependencies. The wiki explicitly says: "Reference the `Pathoschild.Stardew.ModBuildConfig` NuGet package to automatically add the right references depending on the platform the mod is being compiled on."

**Action in Construction**: Add to both `Dayswork.csproj` and (with platform variation) the test project where applicable. `Dayswork.Core` does NOT need this package since it doesn't reference Stardew.

---

### V3 — `MultiplayerGuard` implementation is one line

**What we designed**: `MultiplayerGuard.IsSinglePlayerSession()` — implementation was vague.

**Verified implementation**:
```csharp
public bool IsSinglePlayerSession() => !Context.IsMultiplayer;
```
- `Context.IsMultiplayer` is true if the world was loaded in multiplayer mode **OR** split-screen is active (per the Utilities wiki page).
- `Context.IsMainPlayer` is **also true in single-player**, so it's the wrong test for our FR-MP-01 ("refuse to load in MP") guard.

No design-level change. Just locks in the implementation choice for Construction.

---

### V4 — `PathFindController` namespace moved in 1.6

**What we designed**: `PathFindControllerAdapter` wraps Stardew's `PathFindController`.

**1.6 change**: The class is in **`StardewValley.Pathfinding`** namespace (not `StardewValley` as in 1.5).

**Constructor overload to target** (from decompiled 1.6 source via search):
```csharp
new PathFindController(
    Character c,
    GameLocation destinationLocation,
    Point targetPoint,
    int finalFacingDirection,
    PathFindController.endBehavior endBehaviorFunction = null,
    int limit = 10000,
    bool eraseOldPathController = false)
```

**Action in Construction**: `PathFindControllerAdapter.PathTo(TileCoord)` calls this constructor with an `endBehavior` callback that triggers our `OnArrived` event. Note: if no path is found, the controller silently does nothing — our adapter must detect that case (likely by polling the worker's tile position against expected progress) and fire `OnNoPathFound`. This becomes a Functional Design concern in the Worker / Pathfinding unit.

---

### V5 — Stardew 1.6 changed collections from `List` to `HashSet`

**What's affected**: `Farmer.mailReceived` (and other fields like `achievements`, `professions`, `worldStateIDs`) are now `HashSet<string>` in 1.6, not `List<string>`. Per the migration wiki: "hash sets can't be indexed."

**Action in Construction**:
- `MailDispatcher` "already sent this mail" check uses `Game1.player.mailReceived.Contains(mailId)`, not indexed access.
- Don't write code that does `mailReceived[0]` or `mailReceived.RemoveAt(...)`.
- Use `.Add`, `.Contains`, `.Remove`, `.RemoveWhere`.

---

### V6 — Use `QualifiedItemId` for all item references

**What we designed**: `BufferedItem` in `ItemBuffer` holds "an item reference" — we didn't specify the format.

**1.6 change**: Every item now has both an `ItemId` (string) and a `QualifiedItemId` (globally unique string, e.g. `(O)16` for wild horseradish object, `(BC)128` for furnace big craftable). The wiki migration page emphasizes using `QualifiedItemId` for cross-type unique lookups.

**Action in Construction**:
- `BufferedItem` in [component-methods.md](component-methods.md) should hold `string QualifiedItemId` + `int Stack` + `int Quality`, not a raw numeric ID.
- `ItemRegistry.Create(qualifiedId, count)` is the 1.6 idiomatic way to instantiate an `Item` from an id when depositing.
- Update Core's `Domain/` types accordingly during Functional Design for the Item Buffer / Deposit Planner unit.

---

### V7 — Harmony: "should be a last resort"

**What we have**: Exactly one Harmony patch (BulletinBoardPatch), confined to the `Patches/` namespace per NFR-MAINT-04. This is already aligned with wiki guidance.

**Action**: No design change. Just be explicit in our docs that we deliberately keep Harmony usage minimal and prefer SMAPI events for everything else (which we already do — only the bulletin-board injection point lacks a SMAPI event hook).

**Try-catch wrapper required**: Per the wiki, every Harmony patch method must wrap its body in try-catch and default to running the original logic. Our `BulletinBoardPatch.Postfix_*` methods must follow this pattern. This is a Code Generation concern, surfaced here so it doesn't get forgotten.

---

### V8 — `ModContent` / `GameContent` distinction (1.6)

**What's affected**: When the worker NPC loads its placeholder sprite, the correct API is `Helper.ModContent.Load<Texture2D>("assets/farmhand-sprite.png")` (loading from the mod's own folder). For accessing vanilla assets (e.g., reading the bulletin-board background), use `Helper.GameContent`.

**Action in Construction**: Confirmed for the Worker NPC unit's sprite loading. No design-level change.

---

## Part 3 — One real decision needed before Construction

### V9 — Mail with attached items: pick a delivery strategy

**The issue**: Our design has `MailDispatcher.QueueOverflowMail(items)` — but Stardew's vanilla mail system was historically designed around **pre-authored letter content** (in `Data/Mail`), where attached items are baked into the letter template. Attaching arbitrary runtime items to a letter is not a clean vanilla operation.

**Correction to my first pass**: I initially recommended option C (token injection via `AssetRequested`), but on closer reading of the wiki the vanilla `%item id ... %%` token with multiple items **picks one set RANDOMLY**, not all together — the wiki says explicitly: *"If multiple items are listed (e.g., `%item id (BC)12 3 (O)34 5 %%`) one set will be picked randomly."* That breaks our overflow use case (we want all items in one letter).

**Three viable approaches**:

**A) Depend on Mail Framework Mod (MFM) as a required dependency** — Mail Framework Mod's JSON `Attachments` array supports multiple items per letter natively (`Type` / `Name` / `Index` / `Stack` per attachment). Adding it to `manifest.json` `Dependencies` means SMAPI surfaces a clear missing-dependency message to players who don't have it installed. *Trade-off*: another moving part; our quality is partially coupled to MFM's release cadence.

**B) Build our own minimal mail-with-attachments helper inside the mod** — Harmony-patch the letter-reading flow ourselves to inject attached items when our specific mail IDs are opened. *Trade-off*: more code, second Harmony patch (we said we wanted to minimize Harmony surface).

**C) AssetRequested + vanilla `%item id` tokens** — rejected per the correction above; vanilla token semantics don't give us all items in one letter.

**Decision: A (Mail Framework Mod)** — user choice 2026-05-18.

**Reasoning**:
- Directly satisfies the requirement (multi-item attachments in a single letter) with no extra code surface.
- Mail Framework Mod has been the de-facto standard for runtime mail-with-items since 2018 and is actively maintained.
- Aligns with the "just-in-time onboarding" preference (Q5) — less unfamiliar SMAPI surface to debug.
- One required dependency is acceptable for a niche feature; SMAPI's dependency-resolution UX handles missing-MFM cases automatically.
- Reversible: keeping a clean `IMailDispatcher` interface lets us swap to B in v2 if MFM ever stalls.

**Implications for the design**:
- `manifest.json` must declare MFM as a required dependency (UniqueID + minimum version — to be confirmed during Code Generation of the manifest unit).
- `M-16 MailDispatcher` becomes a thin adapter over MFM's API (acquired via `Helper.ModRegistry.GetApi<...>("DIGUS.MailFrameworkMod")` or whatever the MFM UniqueID is — to be verified in Construction).
- `NFR-COMPAT-04` in requirements.md updated to list MFM as a required dependency.
- Construction note: we should `gh repo`-clone or otherwise vendor MFM's public API interface stub at the start of the Mail unit, same pattern as GMCM.

---

## Part 4 — Items I couldn't verify online (need Construction-phase confirmation)

These weren't blockable, but flagging so they're not forgotten:

- **`IClickableMenu` override list and gamepad-specific overrides** — wiki pages 404'd; the `Modder_Guide/APIs/Menus` page either doesn't exist or moved. Confirmed only the basics (`draw`, `receiveLeftClick`, `Game1.activeClickableMenu`). The full set of gamepad-specific overrides (`receiveGamePadButton`, `populateClickableComponentList`, `snapCursorToCurrentSnappedComponent`, `setCurrentlySnappedComponentTo`, `customSnapBehavior`) needs Functional Design verification for the UI units. This is the area most exposed to surprise.

- **Custom NPC spawning specifics** — Adding a non-villager NPC to the farm location (lifetime management, vanilla NPC-list integration, save-data interactions) — couldn't find a clean canonical reference. The 4 menu / Worker units will need a deeper read of the 1.6 source code or a working community example (e.g., look at how the Stardew Farmhand-like mods handle NPC lifecycle).

- **GMCM `IGenericModConfigMenuApi` exact signatures** — Source fetch failed via both `WebFetch` and `gh`. Surfaced this in [components.md](components.md) M-17 as needing the standard SMAPI pattern of copying the interface stub into our project. The Construction step for the GMCM unit should start by `gh repo clone`-ing the GMCM repo to copy the latest `IGenericModConfigMenuApi.cs` verbatim.

- **Bulletin board class name and patch points** — Likely `StardewValley.Menus.Billboard` based on the 1.5.6 decompile but not confirmed for 1.6. Construction Step 2 (BulletinBoardPatch unit) must verify by inspecting the live 1.6 decompile before writing the Harmony patch signature.

---

## Net impact on the existing design artifacts

- **[components.md](components.md)**: No structural changes. Add small clarifying notes to M-02 (Harmony csproj flag), M-09 (sprite via ModContent), M-11 (PathFindController namespace + constructor signature), M-16 (mail-with-items strategy depends on V9 decision), M-17 (must vendor `IGenericModConfigMenuApi.cs`), M-18 (`!Context.IsMultiplayer` is the implementation).
- **[component-methods.md](component-methods.md)**: Update `BufferedItem` shape to hold `QualifiedItemId` (per V6). Update `MailDispatcher.QueueOverflowMail` doc to reference V9's chosen strategy.
- **[services.md](services.md)**: No service-flow changes; the verified event names match what we wrote.
- **[component-dependency.md](component-dependency.md)**: Add a SMAPI-side dependency from `MailDispatcher` to `Helper.Events.Content.AssetRequested` if V9 = C is chosen.
- **[application-design.md](application-design.md)**: Add a pointer to this verification notes file at the top.

---

## Summary scorecard

- ✅ Core architectural decisions (D1–D6) survive verification.
- ✅ Component layout, services, and dependency graph survive verification.
- ⚠️ 8 minor adjustments captured (V1–V8) — mechanical, not architectural.
- ❓ 1 real decision needed from user (V9 — mail attachment strategy).
- 📋 4 items deferred to Construction Functional Design for live-source verification (IClickableMenu gamepad surface, custom NPC spawning, GMCM API stub, exact bulletin board patch points).
