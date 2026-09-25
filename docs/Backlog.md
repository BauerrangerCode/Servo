# Product Backlog

Servitor V0.1 – Epics & User Stories, ordered by delivery priority

This backlog follows the delivery order set by the project owner: Scry first (built and tested standalone), then Build, Import, Scoring, and Play. Bootstrap work (E1) precedes Scry as its strict technical prerequisite. No sprint/calendar schedule is defined here – this is a priority-ordered backlog, not a timeline; sizing ("pts") is relative effort, not calendar time, for sequencing and capacity discussions only.

**Scope:** 7 epics · 31 user stories · 146 points total. See [CLAUDE.md](../CLAUDE.md) for the project-wide Definition of Done, which applies to every story below and is not repeated per story.

## Delivery order

- E1 – Project Bootstrap & Data Pipeline
- E2 – Scry – Rules & Datasheet Browser
- E3 – Build – Army List Builder
- E4 – Import – Army List Text Import
- E5 – Scoring – Mission & VP Tracking
- E6 – Play – Battle Tracker
- E7 – Mockups & Placeholders (explicitly requested for V0.1)

> **Scope note: Scry is limited to Datasheets for V0.1**
> The reference app's Scry screen offers five searchable content kinds (Datasheets, Abilities, Weapons, Stratagems, Enhancements). Only "individual faction datasheets" was explicitly requested. E2 below covers Datasheets only; browsing Abilities/Weapons/Stratagems/Enhancements as their own searchable kinds is a plausible follow-up but is deliberately left out of this backlog rather than assumed in, per the project's Scope Discipline rule ([CLAUDE.md](../CLAUDE.md)).

> **Resolved: import format for V0.1**
> The reference app accepts pasted text from several sources (NewRecruit, GW App, BattleScribe, Listforge, or "other builders"), auto-detected. The project owner confirmed V0.1 needs exactly one of these: Warscribe 40K's own plain-text export, demonstrated by two provided sample army lists (see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 3.1). E4.2 targets this format only; NewRecruit/GW App/BattleScribe-native/Listforge parsing remains explicitly out of scope until requested.

---

## E1 — Project Bootstrap & Data Pipeline

6 stories · 23 pts · folder: Common / RulesData

Foundation every other epic depends on: a running Blazor WASM shell and two parallel mapping pipelines (since merged into one single source of truth, see "Data source merge" below). BSData wh40k-11e (E1.3) supplies the machine-readable constraints Build's (E3) validation engine needs (wargear option limits, DP/enhancement/unit limits per Battle Size, force organization); `core/` and `factions/<faction>/faction.json` under `src/Servitor.App/wwwroot/data/` (E1.3b) supply the rules-text content (stratagems, detachment abilities, enhancements) that Scry/Action Queue (E2, E6.2/E6.2b/E6.3) need. The two sources overlap on basic datasheet data (points, stats, keywords) but neither replaces the other – confirmed by direct inspection, see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12. Not explicitly requested on its own, but a strict prerequisite for delivering Scry (E2) as the first working feature.

### E1.1 Solution & feature-folder scaffold — 3 pts
As a developer, I want a Blazor WebAssembly (standalone, PWA-capable) solution scaffolded with the feature-based folder layout from CODING_GUIDELINES.md, so that every later feature has a consistent place to live from day one.

- ✓ Solution builds with `dotnet build`, zero warnings, nullable reference types enabled
- ✓ Empty feature folders exist for RulesData, ArmyBuilder, ArmyImport, Scoring, BattleTracker, Shared
- ✓ PWA manifest present; app is installable

### E1.2 Domain model for datasheets — 3 pts
As a developer, I want immutable C# records for the datasheet domain (stat line, ranged/melee weapon profiles with keywords, abilities, wargear abilities, core abilities, keywords, composition constraints), so that Scry and every later feature share one datasheet shape instead of each inventing its own.

- ✓ Records match the shape described in [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 5
- ✓ No business logic inside the records (pure data)
- ✓ Unit-tested construction/equality for at least one representative datasheet

### E1.3 BSData mapping layer — 5 pts
As a developer, I want an explicit mapper from a BSData wh40k-11e catalogue file into the domain records from E1.2, so that the internal domain model never depends on BSData's external file shape directly.

- ✓ Mapper is a distinct class/module, not inline deserialization onto domain types
- ✓ At least one full faction catalogue from `data/bsdata/wh40k-11e/` maps successfully end-to-end
- ✓ A malformed/unexpected input field is a handled, logged case, not an unhandled exception
- ✓ Unit-tested against a real sample catalogue file
- ✓ Reads each unit's force/roster-scope `max` constraint plus any Incursion-conditioned modifier generically (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q9) — no hardcoded per-battle-size lookup table

### E1.3b Rules-text mapping layer (stratagems, abilities, enhancements) — 5 pts
As a developer, I want an explicit mapper from `core/core-stratagems.json` and each faction's `factions/<faction>/faction.json` (both under `src/Servitor.App/wwwroot/data/`) (detachment abilities/enhancements/stratagems arrays) into domain records, so that Scry and the Action Queue have real stratagem/ability/enhancement content without depending on this second external source's file shape directly, mirroring E1.3's separation for BSData.

- ✓ Mapper is a distinct class/module from E1.3's BSData mapper, not inline deserialization onto domain types
- ✓ Domain records for Stratagem/Ability/Enhancement are immutable, in the same feature folder as E1.2's datasheet records, with no business logic inside them
- ✓ Inline HTML markup in description fields (the WHEN/TARGET/EFFECT/RESTRICTIONS formatting, `<b>`/`<span>` tags) is normalized into a form the UI can render safely, not passed through as raw untrusted HTML
- ✓ A malformed/unexpected entry is a handled, logged case, not an unhandled exception
- ✓ Unit-tested against `core-stratagems.json` and at least one faction's detachment stratagems/abilities/enhancements

### E1.4 IndexedDB storage service — 5 pts
As a developer, I want an `IRulesDataStore` service backed by a thin JS interop wrapper around IndexedDB, storing the merged rules data as JSON documents under fixed keys next to a manifest (data version + generation time), so that rules data survives reloads and the app works fully offline after first load.

- ✓ Service interface hides IndexedDB/JS interop details from all consumers
- ✓ Stores the output of both E1.3 (BSData-mapped constraints) and E1.3b (rules-text content) as one merged document with one version, not two parallel stores (revised by the data source merge: originally one key per faction)
- ✓ Round-trip test: store the merged data, reload, read it back unchanged
- ✓ No ORM-style dependency added, per CODING_GUIDELINES.md

### E1.5 App shell & primary navigation — 2 pts
As a user, I want a top navigation bar with Build, Import, Scry, Scoring, Play, each routing to an (initially empty) screen, so that the app has a stable navigation skeleton every feature epic can fill in independently.

- ✓ All five routes exist and are reachable
- ✓ Import is the default/landing route
- ✓ No feature logic implemented yet in this story – empty screens only

---

## E2 — Scry – Rules & Datasheet Browser

4 stories · 18 pts · folder: RulesData / Scry

First feature to be built and tested, per explicit priority. Scope for V0.1: Datasheets only (see note above).

### E2.1 Datasheet search & filters — 5 pts
As a player, I want to search datasheets by name or keyword text and filter by role, faction, and Legends status, so that I can quickly find a specific unit or browse a faction's options.

- ✓ Name search and keyword/rules-text search work independently and combined ("All")
- ✓ Role and Faction filters are tri-state chips: neutral → include → exclude → neutral
- ✓ Legends toggle (Current/Legends) changes the result set accordingly
- ✓ Filters combine with AND semantics across facets

### E2.2 Datasheet result list — 5 pts
As a player, I want a results table (Name / Faction / Pts / M / T / Sv / W / Ld / OC) that stays responsive across the full ~2,300-datasheet dataset, so that browsing does not freeze the UI even with no filters applied.

- ✓ List uses virtualization or an explicit render cap with a "narrow your search" hint, not one unbounded DOM render
- ✓ Result count is shown to the user
- ✓ Manually tested with an unfiltered, all-faction search as the worst case

### E2.3 Datasheet detail view — 5 pts
As a player, I want to open a datasheet and see its full stat line, ranged/melee weapon profiles with keywords, abilities, wargear abilities, core abilities, and keywords – all data visible, with the same completeness as BattleScribe's unit view, so that I have the complete rules reference for a unit in one place, with nothing hidden behind further clicks.

- ✓ All sections from [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 5 are rendered when present in the data
- ✓ Every field present in the BSData catalogue for a datasheet is shown somewhere in the view – no silently dropped fields
- ✓ A datasheet with no melee weapons (or no ranged weapons) renders correctly without an empty/broken section
- ✓ Composition constraints (e.g. a per-army maximum) are shown when present in the data
- ✓ A unit with a Duplicated Ability (the same core or weapon ability listed more than once, e.g. two different Scout ranges) shows every instance as its own entry, as-is – no selection/picker UI is built for this case (project owner decision, [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q2, resolved: Warscribe itself leaves the choice to the player rather than offering one)

### E2.4 Characteristics filter — 3 pts
As a player, I want to add an ad-hoc filter on a numeric characteristic (e.g. minimum Toughness or Wounds), so that I can answer questions like "which units have Toughness 10+".

- ✓ At least one characteristic is filterable end-to-end
- ✓ Filter combines with the facet filters from E2.1

---

## Data source merge between E2 and E3 — implemented, follow-ups open

**Status: implemented (unsized, not part of the "7 epics · 31 stories · 146 points" total above). Not yet committed or independently reviewed.**
Raised by the project owner while reviewing E2 (Scry): the BSData wh40k-11e catalogues (E1.3), `core/` and `factions/<faction>/faction.json` (E1.3b) overlap on datasheet data without one replacing the other. They are now reconciled into one merged single source of truth that the app reads instead of the raw files. Design details: [CODING_GUIDELINES.md](../CODING_GUIDELINES.md) "Data & Assets"; current numbers: `merge-report.md` next to the generated data.

**Delivered:**

- New class library `Servitor.RulesData` (domain, mappers, `RulesDataMerger`), console tool `Servitor.DataMerge` and an MSBuild target `MergeRulesData`, so the merge runs at build time and produces `wwwroot/data/merged/{rules-data.json, manifest.json, merge-report.md}`.
- "Merge data" button in the top navigation: runs the same merge in the browser, stores the result in IndexedDB, shows the report and offers it as a download. At startup the newer of the IndexedDB copy and the shipped files wins.
- Raw source files are never modified. `core/` and `factions/` moved into `src/Servitor.App/wwwroot/data/`; BSData stays as the clone in `/data/bsdata/`.
- Join key: faction + normalized name + Legends flag only. Ambiguous matches take the union of stratagem ids; unresolved ids are dropped and reported.

**Merge rules (project owner):**

- BSData is authoritative for units. From the rules-text source, units only receive stratagem ids.
- Stratagems (core and faction) are taken over from the rules-text source and adapted to BSData-style Markdown (When/Target/Effect/Restrictions).
- BSData core rules stay untouched. Core rules that BSData lacks are added from `core/`. Non-unit rules (movement, weapon abilities such as Devastating Wounds or Sustained Hits) come from BSData where present and from `core/` otherwise.

**Open follow-ups:**

- Core stratagem count (resolved by project owner decision): only entries with a CP cost are stratagems. The 30 cost-free Move/Shoot/Fight entries of `core-stratagems.json` became core rules of kind Action (22 after removing duplicates). Weapon rules (e.g. Blast, Hazardous) and unit rules (e.g. Deep Strike) are kinds of their own (`CoreRuleKind`), so datasheets can link to them later. Older duplicates (same name and game mode, lower id) are dropped and listed in the merge report: 28 core stratagems remain. Boarding Actions and Challenger stratagems (14 of the 28) are identifiable by `GameMode` and stay in the data set but are out of scope for V0.1. Tank Shock, Go to Ground and Grenade have no 11th-edition counterpart of the same name; by project owner decision they stay as Standard-mode stratagems.
- Detachments with zero or one stratagem in the source (7 of them, see the merge report) are source gaps, not merge losses.
- BSData vs. factions+core as the unit source: undecided by the project owner; for now BSData is the unit source.
- Army-building (E3) must derive "one unit, several armies" from the rules-text faction files: BSData lists a shared unit (e.g. Astra Militarum units in Genestealer Cults) only under one catalogue.


---

## E3 — Build – Army List Builder

6 stories · 37 pts · folder: ArmyBuilder

Reuses the datasheet domain model and browsing components from E2.

### E3.1 Battle Size, Faction & detachment selection — 5 pts
As a player, I want to pick one of the three official Battle Sizes (Incursion 1000pts/2DP/2 enh, Strike Force 2000pts/3DP/4 enh, Onslaught 3000pts/4DP/4 enh – exact values confirmed in BSData, see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12), then a faction, then a detachment (with DP cost, mission-affinity tag, enhancement count shown), so that my army list starts from a valid, rules-accurate combination for whichever size game I'm playing.

- ✓ All three Battle Sizes are selectable, each enforcing its own points/DP/enhancement-limit values
- ✓ Detachment list only appears after a faction is chosen
- ✓ A "Skip for now" path lets me proceed without finalizing this step
- ✓ Selecting more than one detachment is possible up to the DP cap for the chosen Battle Size

### E3.2 Unit catalog with add/remove — 8 pts
As a player, I want a unit catalog grouped by role (Epic Hero, Character, Battleline, Transport, Fortification, Other) for my chosen faction, with the ability to add units to my list, so that I can assemble an army from the units available to my faction.

- ✓ Catalog groups and per-group counts match the faction's available units
- ✓ A unit row can open its full datasheet (reusing E2.3) and be added to the list
- ✓ Min/max model-count units (e.g. 5/10 models) show a size toggle and support choosing a valid size within range
- ✓ A datasheet already at its per-army cap shows a live ×current/max badge in the catalog (see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 4)

### E3.2b Wargear configuration per unit — 8 pts
As a player, I want to configure a unit's wargear: a single-slot radio choice for unique models (e.g. a Sergeant's weapon), and a quantity-limited stepper for a pool of identical regular models (e.g. "N of 4 models take a Heavy Bolter instead of a Boltgun"), so that my list reflects the actual loadout I plan to field, not just a generic default.

- ✓ Both configuration shapes are supported (single-model radio choice and pool quantity-stepper), matching [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 4
- ✓ A model/unit with no configurable wargear shows no configuration entry point at all
- ✓ Chosen wargear is reflected in the unit's card and in the list's points total
- **Status / scope (project owner decision):** implemented for the two shapes above, read from BSData's selection tree (`BsDataWargearMapper`): weapon slots of unique models (a group of which exactly one choice is taken) and pools of models taking a weapon, with limits that follow the unit size where BSData expresses them as model-count modifiers. About 650 of 1,519 units have options in these shapes. **Not covered** (no wargear button for units that only have these): nested loadout packages, optional single upgrades (yes/no), per-model choices in non-unique models, hidden options, a cap on the sum of several pools. Points: the per-item prices ("per X") of the rules-text source are added when an item's name matches a chosen weapon; about a third of those prices match an option in the supported shapes, the rest have no effect yet.

### E3.3 Running totals & validity indicator — 3 pts
As a player, I want to see live totals for points used, units, models, and wounds, plus a clear valid/invalid indicator for my list, so that I always know whether my list is legal before I try to play it.

- ✓ Totals update immediately on every add/remove
- ✓ Exceeding the points budget (per the chosen Battle Size, E3.1) flips the indicator to invalid with a visible reason
- ✓ A mandatory-attachment violation (e.g. a Support unit with no bodyguard) also flips the indicator, matching confirmed reference-app behavior ([Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12) – this is a real, tested rule, not just points/DP
- **Status:** totals, points-budget check and the valid/invalid indicator are implemented (prices follow the cost brackets by copy number). The mandatory-attachment check is **not yet implemented**: it needs the "Attach Leader" feature, which no E3 story covers — to be scheduled together with it.

### E3.4 Composition rules engine — 8 pts
As a player, I want my list checked against composition rules: points cap tied to Battle Size, detachment DP budget, same-datasheet unit limits (base value per unit from its own BSData constraint, with an Incursion-tier override read from that unit's modifiers — see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q9, resolved), per-unit model-count bands, faction/chapter-keyword exclusions, and enhancement rules (unique per army, EXCEPT "Upgrade"-tagged enhancements which allow up to 3 copies and may go on non-Character units), so that the validity indicator in E3.3 reflects real rules, not just a points total.

**Decided:** the ordinary enhancement-uniqueness rule and the Upgrade exception ship together in V0.1, not staged (project owner decision D11, [Open-Questions-Catalog.md](Open-Questions-Catalog.md)) — the BSData mechanism for both is already generic and no more costly to implement together.

- ✓ Each rule type has its own unit test with a deliberately-broken example, including a keyword-exclusion case, a duplicate-ordinary-enhancement case, and a valid-triple-Upgrade case
- ✓ Same-datasheet unit limit is read per-unit from its base constraint + Incursion modifier (no hardcoded 2/3/4-per-battle-size table)
- ✓ Rules engine is a separate, UI-independent service/class per CODING_GUIDELINES.md
- ✓ Violating a single rule is reported specifically, not just as a generic "invalid"
- **Status / scope:** implemented as separate rule classes in `ArmyBuilder/Validation` (points total, DP budget incl. the single 3 DP detachment in Incursion, same-datasheet limit, model-count sizes, Chapter restriction, enhancement eligibility / uniqueness incl. Upgrade x3 / battle-size limit / detachment membership), each with its own tests. Not part of any E3 story but needed to make the enhancement rules reachable: a minimal per-unit enhancement dropdown in the army list (Characters: all enhancements of the selected detachments; other units: Upgrades only; Epic Heroes: none; enhancement cost is added to the unit's points). **Also implemented (project owner request):** the "must include / cannot include / points cap / unit limit" rules of detachments as editable data (`wwwroot/rules/detachment-rules.json`, see CODING_GUIDELINES.md "Detachment Rules"); 17 rules of 12 detachments are shipped. **Not covered:** Warlord requirements, the Chapter selector of the reference app, and a TAKEN badge (a duplicate is allowed in the dropdown and reported by the rules instead).

### E3.5 Allies — 5 pts
As a player, I want to see which other factions my chosen faction may ally with, and add units from an allied faction, so that I can build lists that use the allies mechanic.

- ✓ Allowed ally factions are shown per the currently selected faction
- ✓ Units added from an ally faction are correctly flagged as allied in the list

---

## E4 — Import – Army List Text Import

3 stories · 15 pts · folder: ArmyImport

Converges on the same Army model produced by Build (E3). V0.1 scope is exactly one source format: Warscribe 40K's own plain-text army list export (see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 3.1 for the confirmed grammar). Not BattleScribe's native roster format, not NewRecruit, not the GW App export – those remain out of scope until requested.

### E4.1 Import entry screen — 2 pts
As a player, I want a landing screen with a paste-army-list textarea, an "Import & Play" action, a "try a sample army" option, and a fallback to Build, so that I have a fast path into the app whether or not I already have a list.

- ✓ Import screen is the default route (matches E1.5)
- ✓ "Build one instead" routes to Build (E3)
- ✓ "Try with a sample army" loads a checked-in fixture army

### E4.2 Parser for the Warscribe plain-text export format — 8 pts
As a player, I want pasted text in Warscribe 40K's own plain-text export format (header block, Char/unit lines with wargear, Enhancement lines, Leading/Led-by cross-references, version footer) parsed into a valid Army using the E3 domain model, so that I can bring in a list exported from Warscribe instead of rebuilding it by hand.

- ✓ A successful parse produces the same Army shape Build (E3) produces, including detachment(s), warlord, enhancements, and each character's led unit
- ✓ Both provided sample exports (2-detachment and 3-detachment Ork lists) parse to a valid, fully-populated Army each
- ✓ Parsed total points match the header's "TOTAL ARMY POINTS" value exactly (sum of unit + enhancement points as a self-check)
- ✓ Unicode punctuation in names/wargear (e.g. curly apostrophes) round-trips correctly, not just ASCII
- ✓ An unparseable or malformed paste is a handled result, not a thrown exception, with a user-facing reason
- ✓ Parsing logic is unit-tested against the two provided sample exports as fixtures, not only hand-written ones

### E4.3 My Collection — 5 pts
As a player, I want a list of my previously built or imported armies that I can reopen, so that I don't lose or have to re-enter armies between sessions.

- ✓ Saved armies persist across a full app reload (IndexedDB, per E1.4)
- ✓ An army can be reopened into Build for editing or opened directly in Play

---

## E5 — Scoring – Mission & VP Tracking

3 stories · 16 pts · folder: Scoring

Depends on a loaded army (E3/E4) and shares its mission-archetype concept with Build's detachment tags (E3.1).

### E5.1 Mission setup wizard — 5 pts
As a player, I want a step-by-step setup: pick both dispositions (resolving a Primary Mission with its scoring rules and a curated deployment/layout choice), then record Attacker/Defender and who takes the first turn, so that the app walks through the same pre-game steps as the physical game, in order.

**Resolved:** the disposition-pair → Primary Mission mapping and each mission's exact scoring values are now sourced from `wahapedia.ru`'s Mission Deck 2026-27 page (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q5, resolved) — no longer an open data question.

- ✓ Mission archetypes are a single shared value type also used by E3.1's detachment tags
- ✓ Both dispositions resolve to one Primary Mission with a name, description, and a structured list of scoring conditions (each with a point value) – not free text
- ✓ Deployment/layout offers a curated set of named options backed by the Mission Deck's 6 named Deployment cards (Tipping Point, Sweeping Engagement, Search and Destroy, Hammer and Anvil, Dawn of War, Crucible of Battle) and their structured battlefield-edge/zone/objective rules, tied to the resolved disposition pair, matching [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 6 (decision D13, [Open-Questions-Catalog.md](Open-Questions-Catalog.md), resolved) — the third-party visual editor itself stays out of scope
- ✓ Attacker/Defender and First-Turn are simple manual choices (the app does not simulate dice rolls)
- ✓ The disposition-pair → primary-mission mapping and each mission's exact scoring values are sourced from the Mission Deck content, not guessed

### E5.2 Round-by-round VP tracking — 8 pts
As a player, I want to record victory points for both sides each battle round, as a composite of Primary VP, Secondary VP, and a Paint bonus, with Primary VP broken down by the mission's individual scoring conditions, so that I can track the game's score the way the physical scoresheet works, not just a single running number.

**Resolved:** the full Secondary Mission deck (over 20 named cards with exact Fixed/Tactical VP values) is now sourced from the same Mission Deck page (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q6, resolved).

**Out of scope:** whether the Paint bonus is a fixed value or format/event-configurable was raised as a question and explicitly ruled out of scope by the project owner (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md), Out of Scope: Painting-bonus configurability) — Paint stays a flat, non-configurable value, no per-event setting is built.

- ✓ Each side's total is shown as Primary + Secondary + Paint, matching [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 6
- ✓ Primary VP has a per-round entry and a capped total; each scoring condition from the mission is individually scoreable with its own point value
- ✓ Secondary VP supports both a Fixed mode (pick 2 named secondaries) and a Tactical mode (draw-2-per-Command-phase deck with discard/replace), with the mode itself user-selectable, and the correct Fixed-vs-Tactical VP value per card where they differ
- ✓ Running totals are visible at all times during the game

### E5.3 End-game scorecard history — 3 pts
As a player, I want to end the game and have a full scorecard saved to my history, so that I can look back at past games.

- ✓ Ending a game persists a scorecard record (mission, dispositions, VP per round and per category, final result)
- ✓ Scorecard history survives an app reload

---

## E6 — Play – Battle Tracker

7 stories · 35 pts · folder: BattleTracker

The most complex epic – integrates the army (E3/E4), rules data (E2), and Scoring (E5). Built last, per explicit priority. BSData (E1.3) has no stratagem or mission rules text ([Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12); the second data source (E1.3b) supplies the stratagem/ability/enhancement content instead – 1,632 stratagem entries with full CP cost and WHEN/TARGET/EFFECT/RESTRICTIONS text. This unblocks E6.2/E6.2b/E6.3's content.

### E6.1 Round/turn/phase state machine — 5 pts
As a player, I want a battle round counter and a phase stepper (Deployment, Command, Movement, Shooting, Charge, Fight, End of Turn) with previous/next controls, so that the app always reflects where we are in the current game.

- ✓ Phase order matches the sequence above exactly, both directions (next/previous), starting with Deployment before the round-based phases
- ✓ Round counter increments/decrements independently of phase state
- ✓ State survives a page reload mid-game (persisted, not just in-memory)

### E6.2 Action Queue — 8 pts
As a player, I want phase-relevant Core Stratagems, Army Rules, and Detachment Stratagems from my army surfaced automatically as the active phase changes, with an option to hide passive auras, so that I don't have to manually recall every timing-sensitive rule on my datasheets.

- ✓ Queue content filters strictly by phase category (E6.1); the Your-Turn/Enemy-Turn tag is informational only, per confirmed Core Rules structure ([Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11) — both tags are shown simultaneously during one player's turn, this is correct behavior
- ✓ The three categories (Core Stratagems, Army Rules, Detachment Stratagems) are distinguishable
- ✓ Each stratagem/rule renders CP cost (if any), category tag, turn-scope tag, WHEN/TARGET/EFFECT (or Passive text), and RESTRICTIONS/SECONDARY EFFECT when present
- ✓ A rule that presents multiple named sub-options (e.g. a doctrine-style choice with 2-3 alternatives) is supported as its own entry shape, not forced into the plain stratagem card layout
- ✓ A rule whose effect persists across phases (e.g. an entered target that lasts until the next Command phase) stays visible/pinned across phase changes instead of disappearing outside its trigger phase
- ✓ Auras toggle hides/shows aura-tagged abilities without affecting other queue entries
- ✓ CP balance (both players gain 1 CP at the start of every Command phase, per [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11) is tracked and spent when a stratagem is used, not just displayed as a static cost label
- ✓ Queue is driven by the E2 datasheet/stratagem data, not a separate hand-maintained list

### E6.2b Stratagem/ability usage-limit tracking — 5 pts
As a player, I want the app to track and enforce that I cannot use the same stratagem twice in one phase, and cannot target the same unit with more than one stratagem in the same phase (per [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11), so that I don't have to remember these usage limits myself – Servitor tracks real state here rather than staying a passive reference like the observed behavior in Warscribe.

- ✓ Attempting to use the same stratagem a second time in one phase is blocked with a clear reason
- ✓ Attempting to target an already-stratagem-targeted unit again in the same phase is blocked, unless that specific stratagem states otherwise
- ✓ Usage state resets correctly at each new phase/turn boundary (E6.1)

### E6.3 Opponent's Army & Commands sub-views — 5 pts
As a player, I want to view my opponent's army list and a commands/stratagems reference during the game, so that I can check their options without leaving the tracker.

- ✓ Commands view lists the full, unfiltered Core + Detachment Stratagems for the active faction/detachment (confirmed working content shape, see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 7)
- ✓ Opponent's Army opens as a modal (not a routed page) with a Close and a Clear-loaded-army action
- ✓ Opponent's Army loads a second, separately-imported army and shows a compact roster: per unit, name, core stat badges, role/keyword pills, model-count badge, and a compact leader-name badge when a character is attached – no weapon profiles or ability text, per the confirmed reference-app behaviour ([Reference-App-Analysis.md](Reference-App-Analysis.md) Section 7)
- ✓ A collapsible Army Rules section is available for the opponent's army, using the same rule-block rendering as the Action Queue
- ✓ Each opponent unit supports a +Token action and a mark-destroyed toggle, independent of the player's own army state

### E6.4 Empty state — 2 pts
As a player, I want a clear "Import Army" / "Try Demo" prompt instead of a broken tracker when no army is loaded, so that I understand why the tracker isn't showing anything yet.

- ✓ Play screen detects the no-army state and shows the prompt instead of the tracker UI
- ✓ "Try Demo" loads the same fixture army as E4.1

### E6.5 Scoring shortcut inside Play — 3 pts
As a player, I want to reach Scoring (E5) from within Play without leaving the battle tracker context, so that I can update VP without losing my place in the game.

- ✓ Opens the same Scoring feature from E5, not a second implementation
- ✓ Returning from the shortcut restores the exact Play state I left

### E6.6 Per-unit wound tracking with per-model-role granularity — 7 pts
As a player, I want to track wounds per model-role (leader, bodyguard, wargear-bearer) within a unit — not just one aggregate wound count for the whole card — choosing between counting up (damage taken) or counting down (wounds remaining), plus add status tokens or mark a specific model destroyed, so that attached-unit ability persistence is tracked correctly when a specific model dies, matching the Core Rules dependency documented in [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11.

**Scope change (project owner decision D12, [Open-Questions-Catalog.md](Open-Questions-Catalog.md)):** per-model-role tracking is in scope for V0.1, not deferred. Point estimate increased from 5 to 7 to reflect the added model-role granularity over the original aggregate-count design.

- ✓ Each model-role within a unit (leader / bodyguard / wargear-bearer, where applicable) has its own wound-remaining value, both tracking modes (count up / count down) available per role
- ✓ Marking a specific model-role destroyed is distinct from marking the whole unit destroyed, and correctly reflects which attached-unit abilities are lost as a result
- ✓ A unit with no distinct model-roles (a plain squad with no attached character) falls back to one aggregate value, not a forced per-role breakdown
- ✓ Add-token and Destroy actions are available per unit/role and reflected immediately in that unit's card
- ✓ State survives a page reload mid-game (same persistence as E6.1)

---

## E7 — Mockups & Placeholders (explicitly requested for V0.1)

2 stories · 2 pts · folder: Shell / RulesData

Two items the project owner explicitly wants visible now, without working functionality behind them yet.

### E7.1 "Warscribe Apps"-style app-switcher mockup — 1 pts
As a user, I want a switch-app panel listing four other game/edition apps as buttons, so that the UI has the right shape ready for real app-switching later.

- ✓ Panel is reachable from the app shell (header icon, per E1.5)
- ✓ All four buttons are visibly present but perform no navigation or action
- ✓ No other game/edition content or logic is implemented behind them

### E7.2 Core Rules search – empty placeholder — 1 pts
As a user, I want a reachable "search core rules" screen with a search box and no working search yet, so that the entry point exists and a later real implementation is a drop-in addition, not new navigation.

- ✓ Screen is reachable from Play's sub-navigation (matches the reference app's Rules Search location, see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 7)
- ✓ Search box accepts input but returns no results / shows a "coming soon" state
- ✓ No search index or search logic implemented in this story

---

## EPIC — Deferred / Out of Scope for V0.1 (Traceability)

Listed for traceability only – no stories exist for these, and none should be created for V0.1 without an explicit request.

- **Visual theming** toward a Warhammer 40K-styled look. No placeholder requested; only implication for now is to avoid hardcoding styling in ways that would block a later theme layer.
- **AI text-to-speech** reading of rules text on demand.
- **AoS Edition, Spearhead, ToW Edition, Path to Glory** as real, functional apps/features (only their mockup buttons are in scope, E7.1).
- **Cloud sync** (observed as an unconfigured stub in the reference app).
- **Display language switching.**
- **Deployment map / layout tooling (visual editor only)** — the named-layout list itself is now in scope, backed by the Mission Deck's Deployment Cards (decision D13, [Open-Questions-Catalog.md](Open-Questions-Catalog.md), see E5.1). Building an actual visual battlefield/terrain editor (AlpacaSoft's role in the reference app) remains explicitly out of scope.
- **Painting-bonus format/event configurability** — ruled out of scope entirely by the project owner ([Open-Questions-Catalog.md](Open-Questions-Catalog.md), Out of Scope). E5.2's Paint score component stays a flat, non-configurable value; no investigation or per-event setting is planned.

Source: [Reference-App-Analysis.md](Reference-App-Analysis.md) (reference app structure) and the project owner's explicit scope instructions. Re-derive acceptance criteria details from the live reference app where this backlog says "matches the reference app", since exact interaction details were not exhaustively re-verified for every story.
