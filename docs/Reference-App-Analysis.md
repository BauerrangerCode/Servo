# Reference App Analysis

Warscribe 40K (40k.warscribe.app) – Structure & Functionality Reference for Servitor V0.1

This document records the observed UI structure, navigation model, and functional behavior of the reference application Warscribe 40K, as a working reference for implementing Servitor V0.1 (see [CLAUDE.md](../CLAUDE.md), [CODING_GUIDELINES.md](../CODING_GUIDELINES.md)). It describes interaction patterns and data shapes, not Games Workshop's rules text – actual rules content for Servitor is sourced from the BSData wh40k-11e catalogues, not from this reference app (see Section 9).

Captured from the live app on 2026-09-17. The reference app changes over time; treat this as a snapshot, not a live spec – re-verify against the live app before implementing a section if significant time has passed.

## Contents

1. Scope & Out-of-Scope Decisions
2. Global App Shell
3. Import (incl. 3.1 Confirmed V0.1 import format grammar)
4. Build (Army List Builder)
5. Scry (Rules & Datasheet Browser)
6. Scoring
7. Play (Battle Tracker)
8. Cross-Cutting Concerns
9. Data Source Note (BSData vs. this reference)
10. Tech Stack Validation (Blazor WebAssembly)
11. Core Rules Cross-Reference (wahapedia.ru)
12. BSData Investigation & Warscribe Re-Test

---

## Section 1 — Scope & Out-of-Scope Decisions

These decisions were made explicitly by the project owner and apply to everything that follows in this document and in the backlog.

**In scope for Servitor V0.1**
- Full feature parity with the 40K edition of Warscribe: Import, Build, Scry, Scoring, Play.
- The "Warscribe Apps" app-switcher menu, rebuilt as non-functional mockup buttons only (no navigation, no other games implemented).
- An empty placeholder screen/entry point for Core Rules full-text search (no working search behind it yet).

**Explicitly excluded (not even as placeholders)**
- AoS Edition, Spearhead, ToW Edition, Path to Glory – these are separate Warscribe apps for other game systems/formats. Only their entries in the app-switcher mockup are replicated as inert buttons; none of their functionality is analyzed or built.

**Deferred (acknowledged now, not built now)**
- Visual theming toward a Warhammer 40K-styled look & feel. No placeholder needed; the only actionable consequence now is to avoid hardcoding colors/styles in ways that would block a later theme layer (see CODING_GUIDELINES.md Style section on avoiding magic values).
- AI-based text-to-speech reading of rules text on demand. No placeholder needed at this stage.

> **Implementation note: Why Scry first**
> Scry is a read-only query surface over the datasheet/rules data model with no dependency on a user-created army. Building it first gives Servitor a working, testable data pipeline (BSData → domain model → UI) before any of the harder, stateful features (Build, Import, Play) are attempted. Build and Import both converge on the same internal Army model that reuses Scry's datasheet records; Scoring and Play both depend on a loaded Army. This matches the dependency order actually observed in the reference app (see Section 5–7).

---

## Section 2 — Global App Shell

**Primary navigation**
A top tab bar exposes all five primary sections: Build, Import, Scry, Scoring, Play. A separate, narrower bottom navigation bar (used on small/mobile viewports) surfaces only four of these – Build, Import, Scry, Play – with Scoring reachable through Play's own internal sub-navigation instead of getting its own bottom-bar icon (see Section 7).

**Header controls**
- **Switch app** – opens the "Warscribe Apps" panel: links to AoS Edition, Spearhead, ToW Edition, Path to Glory (all excluded, see Section 1).
- **Cloud sync** – shown as "tap to set up", i.e. an unconfigured/optional account-based sync feature. Not explored further; out of scope for Servitor's offline-first goal unless requested later.
- **Display language** – a language switcher for the UI text.
- **Settings** – not explored in depth during this pass.

> **Implementation note: Warscribe Apps mockup**
> Implement this as a static panel with four inert buttons/links labelled AoS Edition, Spearhead, ToW Edition, Path to Glory. No routing, no external navigation, no backing feature – purely visual, per the project owner's explicit instruction.

---

## Section 3 — Import

Import is the app's default landing screen. It offers two ways into the app: paste an existing army list, or start building one from scratch.

- A large textarea ("Army list text") accepts pasted text described as coming from "NewRecruit, GW App, BattleScribe, Listforge, or other builders".
- Primary action **Import & Play** – parses the pasted text and, on success, proceeds directly toward Play.
- **Try with a sample army** – loads a built-in demo army (also referenced from Play's empty state as "Try Demo", see Section 7); useful as fixture data for development and tests.
- Fallback CTA **Build one instead** – routes to Build for users without an existing list.
- **My Collection** – a saved-armies list, reachable from this screen.

> **Implementation note: Multi-format import implies a detection + mapping layer (reference app, not V0.1 scope)**
> Several distinct source formats (NewRecruit, GW App, BattleScribe, Listforge, ...) are accepted through one textarea in the reference app, with no visible format selector observed – so format detection there is automatic. Servitor V0.1 targets exactly one format (see Section 3.1 below); the general principle still applies to that one format: it must map explicitly into the internal domain model rather than being deserialized directly, with a result-style return type for "unparseable input" as an expected, non-exceptional outcome.

### 3.1 — Confirmed V0.1 import format: Warscribe plain-text export

The project owner confirmed V0.1 needs exactly one import format: the plain-text army list export produced by Warscribe 40K itself (identified by its own footer signature, see below), demonstrated by two sample exports of 2000+ point Ork armies. This section documents the observed grammar as a concrete reference for the E4.2 parser (see [Backlog.md](Backlog.md)).

**Header block**
Delimited above and below by a line of repeated `+` characters. Fields observed, one per line, each prefixed with `+`:
- **FACTION KEYWORD** – e.g. "Xenos - Orks" (a faction group and faction name, hyphen-separated).
- **DETACHMENT** – one or more detachment names joined with " + " (observed: two and three detachments in the two samples respectively) – this directly confirms the multi-detachment, DP-budget composition rule inferred from the Build screen in Section 4.
- **TOTAL ARMY POINTS** – e.g. "2005pts"; must equal the sum of every unit's and enhancement's points in the body (useful as a parser self-check).
- **WARLORD** – a reference of the form "Char<N>: <Name>", pointing at one of the character entries in the body.
- **NUMBER OF UNITS** – a plain integer count.

**Body: characters**
Each character (unique/named model) is one line of the form `Char<N>: <qty>x <Name> (<points> pts): <wargear, comma-separated>`, optionally ending in ", Warlord" for the warlord entry. It may be followed by zero or more of these optional lines:
- **Enhancement: `<Name>` (+`<N>`pts)** – the enhancement name may itself carry a parenthetical qualifier before the points, e.g. "'Ardboyz (Upgrade) (+25pts)".
- **Leading: `<UnitName>`** – a forward reference by name to the non-character unit this character attaches to/leads.

**Body: units**
Each non-character unit is one line of the form `<qty>x <Name> (<points> pts): <wargear, comma-separated>`, optionally ending in " [Led by: `<CharacterName>`]" and optionally followed by an Enhancement: line in the same form as above.

> **Implementation note: Leader relationship is recorded twice, in both directions**
> A character's "Leading: X" line and the led unit's "[Led by: Y]" suffix describe the same attachment relationship from both sides. A robust parser can rely on either direction independently and cross-validate the two against each other rather than trusting only one.

**Wargear list**
A comma-separated list of `<qty>x <ItemName>` entries. Quantities are independent per-item tallies (e.g. "18x Slugga, 18x Choppa, 18x Shoota, 2x Kustom Choppa, 2x Kombi-skorcha" for a 20-model unit) and are not required to partition the model count exactly, since a single model can carry more than one wargear item. Item names use proper Unicode punctuation (curly apostrophe ', not ASCII ') – the parser must be Unicode-safe, not ASCII-only.

**Footer**
Three fixed lines: an app/data version line ("App: v<date-version> | Data: v<n>"), "Created with Warscribe: 40K", and the app's URL. This footer is a reliable, unique signature for detecting this specific format if/when additional import formats are added later.

> **Implementation note: Grammar validated end-to-end against the live app**
> The 3-detachment sample (Green Tide + Taktikal Brigade + Madcap Meks, 16 units, warlord, 3 enhancements, 5 character/unit leader attachments) was pasted into the live reference app's Import screen and loaded successfully into Build/Play with every detachment, enhancement, and leader attachment resolved correctly. The grammar documented above is confirmed accurate, not just inferred from the static sample files.

---

## Section 4 — Build (Army List Builder)

This section was re-analyzed with a real army built from scratch (Space Marines, Gladius Task Force, 5 units) rather than just the empty starting screen – every sub-panel below (wargear, enhancements, leader attachment, export) only appears once units actually exist in the list.

**Army Setup**
- **Faction select**: 23 factions. **Chapter select**: a separate field next to it ("Any (Codex)" plus 10 named Chapters for Space Marines) – Faction and Chapter are two independent inputs, not one folded value. Chapter narrows/reskins available options (e.g. Chapter-specific relics/stratagems) and interacts with the keyword-exclusion composition rule confirmed in Section 7 ("Space Marine Chapters").
- **List name**: a third field alongside Faction/Chapter, editable, defaults to the faction name.
- **Detachment select**, shown only after a faction is chosen: each detachment is listed with a "DP" cost, a primary-mission affinity tag (one of the five mission archetypes also used in Scoring, see Section 6), and an enhancement count (e.g. "4 enh"). A header progress bar shows the running total, e.g. "3/3 DP" – a single detachment can already consume the full DP budget (observed: Gladius Task Force alone = 3/3 DP), so the cap is not automatically an invitation to mix several detachments, just a ceiling. Once chosen, the detachment name, its DP cost, its mission tag, and a "view detachment rule" expand are shown persistently, with an "Add / Manage detachments" control for the multi-detachment case.
- A **Skip for now** option lets the user proceed without finalizing faction/detachment immediately.

**List action row**
Once a faction is chosen, a row of actions/tabs appears above the unit list: Faction Rule, Stratagems (N) (opens a modal listing the current detachment's own stratagems as colour-coded cards – header colour keyed to phase, a small type caption such as "Battle Tactic"/"Epic Deed"/"Strategic Ploy", CP cost, then WHEN/TARGET/EFFECT), Allies, a Compact display toggle, Update / Save as new (list persistence), Export, Use List (send this list into Play), and a Saved Lists (3/20) counter – confirms a hard cap of 20 saved lists.

**Unit catalog**
- Units are grouped by role: Epic Hero, Character, Battleline, Transport, Fortification, Other (Fortification only appears for factions that have one), each group showing a count, e.g. "CHARACTER (23)".
- Each unit row shows: name, role/type keyword tags, a model count (fixed or a min/max range), and points (single value or a paired min/max matching the model range). A catalog-wide search box is scoped to the faction (observed: "Search 103 units…" for Space Marines).
- A datasheet already at its per-army cap shows a live ×current/max badge directly in the catalog (observed: "×1/3") – the same "MAX N PER ARMY" constraint from the datasheet detail (Section 5) is enforced and visible while browsing, not just discovered on add.
- A Legends toggle (Current vs. Legends) and a role-filter tab strip mirror Scry's role filters.
- An Allies note states which other factions may be included (e.g. "Your army can also take Imperial Agents · Imperial Knights") with a "Browse" link.

**Adding a unit & configuring it**
- A variable-size unit shows a two-way size toggle (e.g. [5] [10]) directly on its list entry; points update immediately with the chosen size.
- Wargear configuration opens a modal per unit (header: unit name, model count, a "STATS" shortcut to the full datasheet) with a collapsible "Wargear options (datasheet)" section, showing the default loadout as a one-line summary. Two distinct substitution shapes were observed within the same unit: (1) a single unique model (e.g. a Sergeant) gets one or more independent weapon-slot groups, each a mutually-exclusive radio choice among named options; (2) a pool of identical regular models gets a quantity-limited stepper per optional item (observed as "Replace Boltgun with: 0/4 · Heavy bolter" with +/− controls), capped by how many non-unique models exist in the unit. A model with no configurable options (e.g. a fixed-loadout vehicle) shows no Wargear button at all.
- Enhancement assignment opens a search/list modal per eligible character, each entry showing points cost and full rules text. An enhancement already applied to a different model in the same army is shown with a **TAKEN** badge and cannot be picked again – enhancements are unique per army, not per detachment or per model (refined in Section 11: this applies to ordinary enhancements only, not Upgrade-tagged ones).
- Attach leader opens a modal listing only characters eligible to lead the target unit (rules-filtered, not the full character roster), each with a live preview of the abilities the unit would gain if attached, and a single ATTACH action. **Confirmed by live re-test (2026-09-17):** this single "Attach Leader" modal lists both LEADER-tagged and SUPPORT-tagged eligible characters together, each with its own role badge and independent Attach button — there is no separate "Attach Support" flow. Both a Leader and a Support character can be attached to the same unit simultaneously (verified: Victrix Honour Guard with both Cato Sicarius [Support] and Captain Titus [Leader] attached at once, card reading "Led by Cato Sicarius, Captain Titus"). See [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q1, resolved.

**List summary / validity footer**
- Running totals: points used vs. budget (e.g. "700 / 2000"), Units, Models, Wounds, and DP (e.g. "3/3") together, plus a live validity indicator (observed: "VALID").

> **Implementation note: Build is the most rules-heavy screen**
> The live validity indicator, the per-datasheet ×current/max badges, the DP budget, and two distinct wargear-substitution shapes (single-model radio choice vs. quantity-limited group stepper) together imply a real composition-rules engine behind the UI: points cap, per-unit model-count/points bands, detachment DP budget, per-datasheet army-wide limits, enhancement uniqueness, and keyword-exclusion (Section 7). This is exactly the surface CODING_GUIDELINES.md's RulesEngine-style feature folder should own; Build's UI should stay a thin consumer of that engine's validation result, not reimplement the checks itself.

**Export**
The Export panel offers considerably more than the one format Servitor targets for V0.1 (Section 3.1) – worth knowing about even though only one is in scope now:
- **Copy to clipboard**, five text formats: WTC-Compact ("tournament standard"), GW app (Games Workshop app format), Warscribe ("readable, full detail" – the format documented in Section 3.1), Warscribe Compact ("one dense line per unit"), Simple ("squished, no weapons").
- **Export & share**: Tabletop Simulator (a Yellowscribe code or file download – a third-party VTT mod integration), QR code, Print / PDF (a printable roster).

> **Not in scope for V0.1 – noted for completeness only**
> The project owner confirmed V0.1 needs exactly one import format (the Warscribe text export, Section 3.1). The other four export formats and the Tabletop Simulator/QR/PDF outputs are listed here so they are not rediscovered by accident later and mistaken for something already decided; none of them are backlog items.

---

## Section 5 — Scry (Rules & Datasheet Browser)

Scry is a search/browse surface over the entire rules dataset, independent of any army the user has built or imported.

**Search surface**
- "Looking for" switch selects the data kind being searched: Datasheets, Abilities, Weapons, Stratagems, Enhancements – five distinct searches, each over its own content collection with its own filter set and result columns (all five confirmed in this pass, see "The other four content kinds" below).
- Two text inputs: "Search by name" and "Search rules text" (keywords), plus an "All" mode that searches both.
- Facet filters (Datasheets kind): Role (18 tags, e.g. Character, Infantry, Vehicle, Fly, Psyker, ...), Faction (29 entries – notably more than Build's 23, since Space Marine successor chapters are listed separately here), a Legends toggle (Current/Legends), and an open-ended Characteristics filter ("add characteristic filter").
- Filter chips are explicitly tri-state, per the UI's own label: "tap once to include, again to exclude" – i.e. each chip cycles neutral → include → exclude, not a plain on/off toggle.

**Result list**
A table with columns Name / Faction / Pts / M / T / Sv / W / Ld / OC. The result count is shown up front (observed: "2,319 results") and rendering is explicitly capped with a hint to narrow the search ("Showing 100 of 2,319, narrow the search to see the rest") rather than rendering the full set or paging through it – implying either a hard render cap or virtualization tied to the active filters.

**Datasheet detail view**
Selecting a result opens a detail view with the following structural sections (content is Games Workshop's copyrighted rules text and is intentionally not reproduced here – only the shape of the data is described):
- A stat line: Movement, Toughness, Save, (optionally) Invulnerable save, Wounds, Leadership, Objective Control.
- One or more weapon profiles, grouped under RANGED and MELEE headings. Ranged profiles show Range/Attacks/Ballistic Skill/Strength/AP/Damage; melee profiles show Attacks/Weapon Skill/Strength/AP/Damage (no Range). Each weapon carries its own set of keyword tags (e.g. modifiers like "Pistol", "Melta X", "Devastating Wounds", "Precision", "Hazardous", "Torrent", "Lethal Hits" were observed as tag-like keyword badges on individual weapons, not as free text).
- A named-abilities section (ability name + descriptive rules text).
- A separate Wargear Abilities section (abilities tied to specific equipment rather than the unit generally).
- A Core Abilities section (observed value: a "LEADER" tag on a character datasheet).
- A Keywords section (e.g. base size, unit-type keywords, faction keyword).
- An army-composition constraint shown directly on the sheet when applicable (observed: "MAX 3 PER ARMY").

> **Implementation note: Scry defines the core Datasheet domain shape**
> The detail view above is effectively the full shape the internal "Datasheet" domain record needs: stat line, a list of weapon profiles (ranged/melee, each with its own keyword set), a list of named abilities (plus the wargear/core-ability subgroups), a list of keywords, and optional composition constraints. Modeling this once, as pure immutable records per CODING_GUIDELINES.md, and building Scry purely as a query/render layer over it, gives Build, Import, and Play a data model they can all reuse rather than each defining their own.

**The other four content kinds (confirmed structure)**
V0.1 only needs Datasheets (Section 1). The other four were opened to confirm their filter/column shape for future reference – none of this is V0.1 scope:
- **Abilities** (8,635 results): filters Phase and Source (Core / Datasheet / Detachment / Faction – the same four categories seen in Play's Action Queue, Section 7). Columns: Name, Source (the owning unit's name), Faction, Phase, Type (Core/Datasheet/Faction).
- **Weapons** (12,812 results): filters Type (Melee/Ranged) and a large Has ability keyword list. Columns: Name, Unit, Type, Rng, A, BS/WS, S, AP, D.
- **Stratagems** (1,811 results): filters Phase, Turn (Any/Enemy/Your), Type (Battle Tactic / Epic Deed / Strategic Ploy / Wargear – the real 10th-edition stratagem categories), and Faction (including a pseudo-faction "Core" for faction-agnostic stratagems). Columns: Name, Detachment, Faction, CP, Phase, Turn.
- **Enhancements** (1,400 results): filters Faction only. Columns: Name, Detachment, Faction, Pts.

> **Data-quality note for the BSData mapping layer**
> The Weapons kind's "Has ability" filter list contains multiple casing variants of what is clearly the same keyword (e.g. "Devastating Wounds", "DEVASTATING WOUNDS", "Devastating wounds" all appear as separate filter options; similarly several "Anti-*" variants). This suggests the underlying rules data is not perfectly normalized. Servitor's BSData mapping layer (CODING_GUIDELINES.md) should normalize keyword casing during mapping rather than trusting the source text verbatim, or filtering/search will silently miss matches.

---

## Section 6 — Scoring

This section was re-analyzed by actually completing the mission setup wizard and reaching live VP tracking, not just the initial disposition pickers.

**Mission setup wizard**
- **Your Disposition / Enemy Disposition**: pickers for the five mission archetypes also seen as detachment tags in Build (Take and Hold, Disruption, Priority Assets, Purge the Foe, Reconnaissance).
- Once both are picked, a Primary Mission is resolved immediately: a named mission with flavour text and a structured "How to score" list of scoring conditions, each with an explicit point value (e.g. "+3 …", "+5 …"). Content is Games Workshop's copyrighted mission text and is not reproduced here – only the fact that it is structured data (condition text + point value per entry), not flavour prose.
- **Deployment & layout**: a curated, selectable list of named map/layout options specific to the resolved disposition pair (observed: 3 options, each labelled with the disposition pair and a layout letter, e.g. "… - Layout A/B/C") – an actual dropdown, not just static text. The external link ("AlpacaSoft's Layout Editor") remains alongside it, credited to a third party.
- **Step 1 – Attacker / Defender**: explains the physical D6 roll-off, then a manual "You are the… Attacker / Defender" choice records the outcome (the app does not roll dice for you here).
- **Step 2 – First Turn**: same pattern – explains the roll-off, then a manual "Who takes the first turn? You / Enemy" choice.
- Completing this wizard hands off directly into Play: Play's Deployment Phase checklist and the round/phase tracker become active immediately after (confirmed by observation – Play showed the live Command-phase queue right after finishing this wizard).

> **Implementation note: Mission-selection mechanism vs. the mission table itself**
> The wizard confirms the mechanism inferred earlier from Play's Deployment checklist: both dispositions are entered as the outcome of a secret-pick-and-reveal step, and that pairing resolves deterministically to one Primary Mission with its own scoring rules and a curated set of deployment layouts.
>
> **Update:** the disposition-pair → mission lookup table itself and the exact scoring-condition values for every mission are now resolved — see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q5, sourced from `wahapedia.ru`'s Mission Deck 2026-27 page. E5.1 is unblocked.

**Live VP tracking**
- Each side's score is a composite of three parts, shown as e.g. "10 (0+0+10)": Primary VP + Secondary VP + a Paint bonus (observed: 10 VP for a fully painted army – the standard matched-play painted-army bonus). A live-tested CHALL bonus (see below) adds a 4th term when non-zero. Whether the Paint bonus itself is always a flat 10 VP or format/event-dependent was raised as a question but the project owner ruled it out of scope for Servitor — E5.2 keeps this a flat, non-configurable value; see the Out-of-Scope note in [Open-Questions-Catalog.md](Open-Questions-Catalog.md).
- A round tracker strip (rounds 1–5) shows whose turn is active and a tied/leading indicator.
- Primary VP is tracked with an explicit cap (observed: 0/45 total) and a per-round sub-entry (observed: "Round 1: 0/15"). Each scoring condition from the mission's "How to score" list is rendered as its own actionable entry with timing metadata (e.g. "Any battle round" / "End of your turn") and its point value, plus a full per-round history table (R1–R5 columns + total) for both sides. **Confirmed by live re-test:** cumulative "for each X" conditions render as a −/count/+ stepper (not a plain checkbox) and update the round/total/composite score instantly on click, with no phase gating; boolean end-of-battle-only conditions render as a checkbox that stays genuinely disabled until the relevant round is reached. See [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q3, resolved.
- Secondary VP supports two selectable modes: **Fixed** (pick exactly two named secondary missions from faction-scoped dropdowns, e.g. "Secondary 1" / "Secondary 2") and **Tactical** (a card-deck mechanic: draw 2 cards at the start of your Command phase, discard-and-replace one for 1CP once per battle, search/add a specific card manually, with a remaining-deck counter, e.g. "18 cards remaining"). The full deck contents and exact VP values for both modes are now known — see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q6.
- A **DETAILS** tab and a **CHALL** counter ("+10 CHALL 0" per side) were also observed on the score header, with its own +/− stepper, adding directly to the composite score as a 4th term alongside Primary/Secondary/Paint. **Resolved by live re-test:** CHALL is the **Challenger card** catch-up mechanic from the 2025-26 Chapter Approved Mission Deck — a player trailing by 6+ VP draws a Challenger card each Command phase for a free Stratagem or a bonus scoring option; Warscribe can't detect the VP gap or which option was picked automatically, so it exposes CHALL as a manual counter. See [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q4, resolved.
- An **End Game** button is present throughout, consistent with the earlier-documented "End the game to save a full scorecard to your history" behaviour.

> **Implementation note: Shared Mission concept**
> The five mission-archetype labels appear identically in both Build (as a detachment tag) and Scoring (as a disposition choice). Model this as a single shared enum/value object, not two independent representations that could drift apart.

**Deployment maps: reframed by a newly-found real data source**
The reference app itself does not build deployment-layout tooling; it curates named layout options and links to a third party for the visual editor. The Mission Deck page additionally documents a real, structured **Deployment Card** mechanism (6 named cards, each with battlefield-edge/zone/objective-placement rules) as the "Custom" alternative to the app's "Recommended layout (GW App)" option — this is a genuine data source, not just a third-party link. Whether Servitor should consume this content for E5.1's curated layout list is now a scope decision for the project owner rather than a missing-data problem — see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) D13. The visual deployment-map editor itself remains out of scope regardless.

---

## Section 7 — Play (Battle Tracker)

Play is the most complex section: it integrates an army (from Build or Import), the rules data (from Scry's underlying model), and the mission/scoring state into a live, turn-based tracker. This section was re-analyzed with a real 16-unit, 3-detachment Ork army loaded (see Section 3.1) – the empty-state pass alone had missed most of what follows.

**Empty state**
Without a loaded army, Play shows "Import Army" / "Try Demo" prompts instead of the tracker – i.e. Play has a hard dependency on an Army being present. This confirms the build order: Import and Build must exist before Play can be meaningfully implemented or tested end-to-end.

**Turn/phase state**
- A battle round counter (e.g. "R1") with increment/decrement controls.
- A turn indicator ("My Turn" – an opposing-turn state is implied but not observed directly).
- The phase sequence begins with a distinct Deployment Phase before the round-based phase stepper takes over: Deployment → Command → Movement → Shooting → Charge → Fight → End of Turn, with Previous/Next controls – a finite-state phase machine, not just the six in-round phases assumed in the earlier empty-state pass.

**Deployment Phase content**
Rather than an empty step, Deployment renders a structured, ordered pre-game checklist (drawing a deployment card, agreeing battlefield size/edges, secret disposition pick-and-reveal, secondary mission setup, alternating unit deployment, roll-off for first turn, pre-battle rules) with an explicit "move to the Command phase once deployment is complete" exit condition. Content is Games Workshop's copyrighted core-rules text and is intentionally not reproduced here – the structural point is that Play surfaces the entire pre-game sequence as actionable steps, not just the six combat-round phases.

**Action Queue: three content categories, one stratagem shape**
The queue is not one flat list; it groups content under three labelled headings, all rendered with the same underlying stratagem/ability shape:
- **Core Stratagems** – universal, available to every army regardless of faction/detachment (e.g. a generic re-roll stratagem valid in any phase).
- **Army Rules** (labelled "Unit Abilities" for at least one faction observed, see note below) – a mix of faction-wide passive rules, per-unit passive abilities relevant to the currently active phase (grouped under the owning unit's name), and army-wide "Ready"-type abilities with their own activation button (once-per-battle effects, distinct from always-on "Passive" ones).
- **Detachment Stratagems** – specific to the chosen detachment (only surfaced when that detachment is part of the loaded army).

> **Implementation note: Section heading varies by faction; a targeted Army Rule needs its own unit-picker UI**
> With an Ork army loaded this section was headed "Army Rules"; with a Space Marines army it was headed "Unit Abilities" – same underlying category and schema, the heading text adapts to content rather than being a fixed label. Separately, at least one Army Rule (Space Marines' detachment-defining ability) is shown with a "No Target" badge plus a "Select target unit" control: unlike passive abilities, this kind of rule needs the player to pick a unit (in this case an enemy unit) each time it triggers, and the app must remember that selection until it next resets. This is a distinct interaction pattern from every other Action Queue entry and needs its own UI affordance, not just a text card.

> **Implementation note: Confirmed stratagem/ability data shape**
> Every entry above shares one schema: name, CP cost (stratagems only; army rules can cost 0), a category tag (e.g. a phase name like "Movement"/"Shooting"/"Fight"/"Charge"/"Command", or "Any Phase"), an optional turn-scope tag ("Your Turn" / "Enemy Turn"), a WHEN section (trigger condition), a TARGET section, an EFFECT section (occasionally a numbered sequence), an optional RESTRICTIONS section (e.g. a once-per-battle or first-round limitation), and occasionally an optional SECONDARY EFFECT offering an upgraded outcome for an additional CP cost. Passive abilities use the same shape minus the CP cost and WHEN/TARGET/EFFECT split, replaced by a single "Passive" tag plus descriptive text. This is the confirmed shape for the Stratagem/Ability domain records needed by E6.2/E6.3 (see [Backlog.md](Backlog.md)) – not reproduced verbatim here since the actual rule text is Games Workshop's copyrighted content. Scry's own Stratagems search (Section 5) independently confirms Phase, Turn, CP, Faction, Detachment and a Type facet (Battle Tactic / Epic Deed / Strategic Ploy / Wargear) as real structured fields, not just badges rendered from free text.

- An **Auras** toggle hides/shows passive aura-type abilities from the queue.
- A fullscreen mode for the queue is available.
- The queue updates automatically as the active phase changes, filtering to what's relevant right now rather than showing everything at once.

> **Turn-scope tag does not filter visibility — resolved, see Section 11**
> Walking through all seven phases with a loaded army showed the queue filtering strictly by phase category (Command-only entries disappear outside Command, etc.), but not consistently by the "Your Turn"/"Enemy Turn" tag: during "My Turn", the Shooting-phase queue showed both a "Your Turn" stratagem and an "Enemy Turn" one side by side. Section 11 confirms via the Core Rules this is correct, rules-accurate behavior (the tag is a quick-reference legend, not a hard display filter) — not an app inconsistency. E6.2's acceptance criteria state this as confirmed.

> **Implementation note: A detachment rule can present a multi-option choice, not just a single toggle**
> Space Marines' "Combat Doctrines" (a Gladius Task Force detachment rule) is one Action Queue entry that contains three named, individually described sub-options (Devastator/Tactical/Assault Doctrine), each visually colour-coded, with the rule stating only one may be selected per Command phase and each only once per battle. This is a distinct interaction shape from every other entry documented above (plain passive, CP-cost stratagem, or Oath-of-Moment-style target entry) and needs its own "pick one of these named sub-options" UI, not just a button.

> **Implementation note: Oath-of-Moment-style rules persist across phases and use free-text entry**
> Re-checked across all seven phases: the Space Marines "Oath of Moment" entry remains pinned in the Action Queue in every phase, not just Command – consistent with it representing a choice that stays in effect "until the start of your next Command phase" rather than a one-off trigger. Its input control is a free-text field ("Tap to enter target unit name…"), not a dropdown as first assumed from the collapsed-panel text alone.

**In-army datasheet rendering (differs from Scry's detail view)**
Every unit in the loaded army is rendered inline below the Action Queue, using the same underlying data as Scry's datasheet detail view (Section 5) but a more compact layout:
- Weapon profiles use one flat table (columns: Rng/Atk/Hit/S/AP/Dmg) with an R or M row-prefix marking ranged vs. melee, instead of Scry's two separately-headed RANGED/MELEE groups – same underlying weapon-profile data, a different layout choice; do not model two different weapon shapes for the two contexts.
- The stat line is not a fixed 7-field set: an optional FNP (Feel No Pain) badge was observed alongside M/T/SV/INV/W/LD/OC on one unit – model the stat line as a base set plus optional extra badges, not a rigid record.
- A Dedicated Transport-capable unit additionally shows a capacity indicator (observed as "0/7", i.e. embarked count / capacity) next to its stat line.
- A vehicle/monster's degrading-profile-at-low-wounds rule is shown as a plain entry in its abilities list (observed: "Damaged: 1-4 wounds remaining"), not as a separate mechanic or table.
- An active enhancement is shown directly on the unit's header as a ★-prefixed tag (e.g. an upgrade name), rather than only in a separate enhancements list.
- A second (or further) copy of an identical unit in the list is disambiguated with a #2-style suffix on its name.
- A wargear item held by multiple models in a unit is shown once with a ×N quantity suffix on its name, with the Atk column already reflecting the unit-wide total rather than a per-model value.
- A single wargear item with more than one firing/use mode (e.g. a combination weapon) is split into separate weapon-profile rows that share a base name with a mode suffix (e.g. "`<Item>` - Mode A" / "`<Item>` - Mode B"), each with its own full profile.
- An attached character is indicated inline within the host unit's card via a LEADER tag positioned among that character's own weapon rows, rather than as a separate cross-reference element.

**Per-unit wound tracking**
- Each unit/model exposes a choice between two wound-tracking modes: counting up (damage taken so far) or counting down (wounds remaining) – a per-unit or per-army preference, not fixed to one convention.
- An **Add token** action and a **Destroy** action are available per unit/model, implying lightweight status-marker support beyond raw wound numbers.

> **Implementation note: Wound tracking was entirely missed in the earlier empty-state pass**
> None of this – the count-up/count-down choice, tokens, or the destroy action – is visible without a loaded army, since there is nothing to track wounds on. This is the clearest example of why Section 7 needed re-analysis with real army data rather than relying on the empty state (see [Backlog.md](Backlog.md) E6.1 for the affected story). Section 11 refines this further: attached-unit ability persistence depends on which specific model-role dies, driving E6.6's per-model-role scope (project owner decision D12, [Open-Questions-Catalog.md](Open-Questions-Catalog.md)).

**Display density presets**
A row of layout options ("Modern", "3-Panel", "All-in-One", "War Organ") appears at the bottom of both the Action Queue and the Commands view. This reads as a global rendering-density preference for how ability/stratagem/datasheet cards are laid out on screen, not a feature-specific control – out of scope to replicate for V0.1 unless requested, but worth keeping card-rendering as its own component so a future density setting isn't a rewrite.

**Sub-navigation within Play**
- Sub-nav (rendered as a left icon rail on wide viewports, a bottom bar on narrow ones): Commands, Opponent's Army, Rules Search, plus a 4th confirmed item Army & Detachment Rules. Commands opens the same Core/Detachment Stratagems content described above, outside the phase-filtered Action Queue (i.e. a full, unfiltered stratagem reference during play). Army & Detachment Rules opens the same rule-block panel used by Opponent's Army (below) but for your own army – one shared "Army Rules panel" component for both sides, not two.
- A separate internal state also exposed Army and Scoring as reachable sub-views in one observed pass – treat Scoring-inside-Play as a shortcut into the same Scoring feature from Section 6, not a second implementation of it.

> **Implementation note: Desktop layout is a persistent 3-column layout, not modals**
> At a wide viewport, Play renders as three permanent columns: a narrow left rail (round/phase controls plus icon buttons for Commands/Opponent/Search/Army Rules), a centre column (Action Queue + full in-army datasheets, Section 7), and a right-hand context panel that opens/closes per the selected rail icon (e.g. Army Rules). The dialog/modal presentation documented for Opponent's Army below was observed at a narrower emulated width; both are almost certainly the same components in a responsive layout (persistent panel when there's room, overlay when there isn't) rather than two different features – design Servitor's own Play screen the same way rather than building two separate implementations for desktop and mobile.

**Opponent's Army (confirmed, with an opponent army loaded)**
"Opponent's Army" opens as a modal dialog over the Play screen (not a routed sub-page), with its own close (✕) control and a "Clear" action to remove the loaded opponent army. Its content is deliberately lighter-weight than your own army's in-Play view:
- Faction name and a collapsible Army Rules section (same rule-block schema as the Action Queue: title, a phase/scope tag such as "Start of Command" or "Passive", and body text with nested sub-bullets) – confirms Army Rules content and schema are shared between "my army" and "opponent's army", not duplicated per side.
- A collapsible section named after the opponent's detachment (e.g. "Gladius Task Force"), containing one compact card per unit: name, a small stat-badge grid (M/T/SV/INV/W/LD/OC), color-coded role/keyword pills (e.g. Character = orange, Psyker = purple, Battleline = green, Infantry = neutral), a quantity badge (e.g. "×5") for multi-model units, and a compact badge naming the attached leader when present (e.g. a small "Librarian" chip) – a different, lighter leader indicator than the inline LEADER tag used in the full datasheet view.
- No weapon profiles or full ability text are shown per unit in this view – it is a roster/reference overview, not a rules reference. Full detail would presumably come from opening that datasheet via Scry if needed.
- Each opponent unit has a +Token button and a small icon toggle that marks the unit destroyed (strikethrough name, icon changes), mirroring the count/token/destroy interactions on your own army's units (Section 7, "Per-unit wound tracking") but as a single-click toggle rather than a separate labelled button.

> **Implementation note: A concrete composition rule surfaced via the opponent's Army Rules block**
> The Space Marines "Army Rules" block observed here includes a composition-constraint entry (tagged "Passive") describing Chapter-keyword mechanics: a unit can carry a second, Chapter-specific faction keyword, and including certain Chapter-specific units (e.g. from Black Templars, Blood Angels, Dark Angels, Deathwatch, Space Wolves) restricts what else the army may contain. This is the actual mechanism behind Build folding those chapters into a single "Space Marines" faction entry (Section 4) – it is enforced via keyword-based exclusion rules inside one faction, not by treating each chapter as a fully separate faction. Relevant to E3.4's composition rules engine (see [Backlog.md](Backlog.md)) as a concrete rule type to support, beyond points/DP/model-count checks.

**Rules Search (the Core Rules search placeholder)**
A dedicated sub-view with a search box, a "CORE RULES" quick-access chip (labelled with an edition/errata code, observed as "CA 25-26"), and placeholder copy: "Search rules, your army's abilities, or all factions." This is the direct analogue of the feature the project owner wants stubbed out now with no working search behind it yet.

> **Implementation note: Core Rules search placeholder – scope for V0.1**
> Build the entry point (a reachable, empty search screen with a search box and a "Core Rules" quick-access affordance) with no working search logic behind it. This keeps the later full-text search feature a drop-in addition rather than requiring new navigation/UI to be retrofitted.

---

## Section 8 — Cross-Cutting Concerns

- **My Collection** (saved/imported armies) is reachable from Import and implied to also be used by Build/Play as the army-selection surface; treat it as a first-class "saved armies" list backed by the same persistence layer as everything else (IndexedDB per CODING_GUIDELINES.md), not a one-off feature.
- **Cloud sync** is present only as an unconfigured stub ("tap to set up"). Out of scope for Servitor's offline-first V0.1 goal unless requested later.
- **Display language** switching exists in the reference app's UI chrome. Not required for V0.1 unless requested; note it as a reason not to hardcode UI strings inline if that is cheap to avoid, but do not build a localization system now (Scope Discipline, [CLAUDE.md](../CLAUDE.md)).
- A first-visit promotional dialog ("Now on Google Play", advertising a native Android app) was observed. Irrelevant to Servitor; noted only so it is not mistaken for a feature to replicate.

---

## Section 9 — Data Source Note

**This document describes UI/UX, not the rules content itself**

Everything above describes the reference app's screen structure, navigation, and interaction patterns – deliberately without reproducing Games Workshop's copyrighted datasheet/ability rules text observed in the reference app. Per CODING_GUIDELINES.md, Servitor's actual rules content comes from the BSData wh40k-11e catalogues (already present under `/data/bsdata/wh40k-11e/` in this repository) and the rules-text source (`/data/core/`, `/data/factions/`), through explicit mapping layers into Servitor's own domain model – not from scraping or copying the reference app's content. Mission content (Section 6, resolved) is likewise sourced directly from Games Workshop's published Mission Deck via wahapedia.ru, not from the reference app. Use this document to build the right screens, interactions, and domain shape; use the local data sources and the Mission Deck page as the actual sources of truth for rules and mission content.

---

## Section 10 — Tech Stack Validation (Blazor WebAssembly)

CODING_GUIDELINES.md already commits Servitor to .NET 10 Blazor WebAssembly (standalone, PWA-capable) — updated from the original .NET 8 target when E1 implementation found only the .NET 10 SDK installed on the development machine. This section checks that choice against what Section 2–7 above actually require, as requested.

**Why it fits**
- **Offline-first goal**: Blazor WASM standalone + PWA caching + IndexedDB (already the chosen persistence approach) is a straightforward, well-supported path to "fully usable offline" – no server round-trip is required for any of the five features described above; all of them operate on locally-held data once BSData has been imported once.
- **Data-shape-heavy domain**: the datasheet/weapon/ability/keyword structure in Section 5, and the composition-rules engine implied by Section 4, are naturally expressed as strongly-typed, immutable C# records with exhaustive matching (e.g. ranged vs. melee weapon profiles, mission archetypes as a closed enum) – a good fit for C#'s type system, and directly aligned with the "pure immutable records, no business logic in data" rule already in CODING_GUIDELINES.md.
- **Single-language project**: feature-based folders spanning UI (.razor) and logic (.cs) in one language keep each feature (ArmyImport, BattleTracker, RulesEngine, ...) self-contained, without a frontend/backend language split – reasonable for a project without a dedicated backend (everything here is client-local).
- **No deep native-device requirement**: nothing observed in Sections 2–7 needs camera, Bluetooth, or other native APIs Blazor WASM struggles with – the app's only device dependency is local storage, which IndexedDB (already chosen) covers natively in-browser.

**Where it genuinely gets harder (named honestly, not a reason to switch)**
- **Cold-load payload size**: the .NET WASM runtime plus app assemblies are historically heavier to download than an equivalent JS SPA bundle, even with .NET's trimming/AOT improvements. As an installed PWA this cost is paid once (cached thereafter), but the very first load – plausibly at a game store with weak wifi – will be noticeably slower than a lightweight JS app. Worth measuring early (e.g. after the Scry vertical slice ships) rather than assuming it away.
- **Large filterable result sets**: Scry's cross-faction search spans all 2,319+ datasheets at once with live text/keyword filtering and a stated render cap ("Showing 100 of 2,319"). Blazor's Virtualize component and in-memory LINQ filtering (already the chosen approach per CODING_GUIDELINES.md) can handle this, but it needs deliberate virtualization/pagination design from the start – this specific access pattern (filter across *all* factions at once, not one faction at a time like Build's catalog) is more battle-tested in JS grid libraries than in Blazor, so budget explicit UI-performance testing for this screen rather than assuming default component behavior scales.
- **Touch/mobile polish**: the reference app is clearly mobile-first (compact bottom nav, tap-to-cycle filter chips) and additionally ships as a native Android wrapper. Blazor WASM inside a mobile browser/PWA is workable, but tap-target sizing, chip-cycling interactions, and scroll performance on long lists need explicit attention – they are not automatically "free" just because the framework is capable.

> **Implementation note: Verdict**
> Blazor WebAssembly is a sound choice for this project's actual requirements (offline-first, data/rules-shape-heavy domain, single-language solo/small-team development, no deep native-API need) and is already a committed decision in CODING_GUIDELINES.md – nothing above is a reason to revisit it. The two concrete risks (cold-load size, the large-result-set Scry screen) are engineering problems to design around during the Scry epic specifically, not blockers to the stack choice itself.

---

## Section 11 — Core Rules Cross-Reference (wahapedia.ru)

Warscribe's terminology and behavior are not app-specific inventions – they follow the official Warhammer 40,000 11th-edition Core Rules essentially 1:1. This section maps the Core Rules (as published on `wahapedia.ru/wh40k11ed/the-rules/core-rules/`, retrieved 2026-09-17) to the Warscribe behavior documented in Sections 2–9, corrects two assumptions made earlier from observation alone, and records what each Section really depends on. As elsewhere in this document, rule text itself is Games Workshop's copyrighted content and is described structurally, not quoted at length.

**Battle round & phase structure (corrects Section 7)**
The Core Rules define a Player Turn as exactly the phase list Warscribe shows: Command, Movement, Shooting, Charge, Fight, and a formal "End of Turn Step" that resolves end-of-turn triggers. Section 7 earlier guessed that Warscribe's "Start of turn"/"End of turn" entries were the app's own bookend additions around 5 "real" phases – that guess was wrong. All 7 stepper entries (Deployment aside, which is its own pre-game step, not a Player-Turn phase) are official rules structure. Model the phase sequence as a direct 1:1 enum against the rules, not as "5 rules phases + 2 UI phases".

**Command Points & Battle-shock (grounds Section 7's Action Queue and Section 4's stratagem cost)**
- Both players gain 1 Command Point at the start of every Command phase – including the opposing player's Command phase, not only the active player's. A CP-tracking service must credit both sides on every Command phase, not just "my" phase.
- The Battle-shock step requires a roll for any unit that is already battle-shocked or at/below half-strength (current models or, for a single-model unit like a vehicle, current wounds vs. starting wounds). This is a direct dependency on live wound state (E6.6, Section 7) – if Servitor ever automates or prompts battle-shock rolls, it needs each unit's current-vs-starting strength, not just a raw wound counter.
- This precisely explains the "Insane Bravery" stratagem's trigger ("just before you make a battle-shock roll") documented in Section 7 without needing its rule text reproduced.

**Stratagems: the WHEN/TARGET/EFFECT card is official structure, not a Warscribe UI choice**
- Every stratagem officially states its CP cost, WHEN, TARGET, EFFECT, and optionally RESTRICTIONS – exactly the fields Section 7 documented. Warscribe is rendering the rules schema, not inventing one.
- Two real usage limits exist that Warscribe's Action Queue does not appear to enforce as state (it is a reference, not a tracker): the same stratagem cannot be used twice in the same phase, and the same unit cannot be targeted by more than one stratagem in the same phase (unless a stratagem says otherwise). The project owner decided Servitor tracks real usage state here (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Category R, E6.2b).
- The rules define a "Used in: Either / Your / Opponent's turn" key as a quick-reference legend, not a hard display filter – the actual condition is always in the WHEN text. This directly resolves the Section 7 finding that Warscribe shows both "Your Turn" and "Enemy Turn" entries together during one player's turn: that is correct, rules-accurate behavior, not an app inconsistency. E6.2's acceptance criteria ([Backlog.md](Backlog.md)) state this as confirmed rather than as an open caveat.
- The stratagem Type facet observed in Scry (Battle Tactic / Epic Deed / Strategic Ploy / Wargear, Section 5) does not appear anywhere on this Core Rules page – it is real 10th/11th-edition terminology, but defined at Codex/detachment level, not in the base rulebook. Servitor's BSData mapping sources it from detachment data, not core rules data.

**Objective Control (grounds Section 6's Scoring conditions)**
- Control of an objective is the sum of the OC characteristic of all models within range, compared between players; ties leave the objective uncontrolled unless it is secured (stays controlled until the opponent's control becomes strictly greater).
- A battle-shocked unit's models have an OC of "–" – they contribute nothing to objective control. This is a second, independent link between battle-shock/wound state and Scoring: a unit reduced to half-strength (and therefore forced to test battle-shock, above) can lose its ability to score "you control an objective" conditions even before failing that test, simply by being at low models. Worth surfacing in Servitor's UI once wound-tracking and Scoring are both live, even though it's not required to make either feature work standalone.

**Attached units: Leader and Support are two different Core Abilities (refines Section 4/7's "LEADER" tag)**
- Core Abilities define LEADER and SUPPORT as two separate abilities; a bodyguard unit can have one of each attached at the same time, not just one "leader slot" total. **Confirmed by live re-test:** Warscribe folds Support attachment into the same "Attach Leader" flow rather than exposing it as a separate action — see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q1, resolved.
- Attachment happens during army building ("Muster Armies"), confirming Build's leader-attach step (Section 4) is rules-accurate in when it happens, not just in what it looks like.
- An attached unit is one unit "for all rules purposes", and ability persistence after a model's death depends on which specific model (the leader, the support, a bodyguard, or a specific wargear-bearer) was destroyed. Exact wound-tracking per model-role inside an attached unit is needed to reproduce this correctly — the project owner decided this is in scope for V0.1 (decision D12, [Open-Questions-Catalog.md](Open-Questions-Catalog.md); E6.6, [Backlog.md](Backlog.md)).

**Ability categories (grounds Section 5's datasheet sections and Section 7's Auras toggle)**
- "Faction Abilities" is the official term for what Warscribe sometimes labels Army Rules (Section 7's earlier note on the heading varying by faction) – same concept, confirmed.
- Aura abilities are a formally tagged category (any ability tagged "Aura") – this is exactly what Section 7's "Auras" toggle hides/shows; it is not a heuristic Warscribe invented.
- Wargear Abilities and Psychic abilities are likewise formally tagged categories, matching Section 5's "Wargear Abilities" datasheet section and the "Psychic" weapon-keyword tag exactly.
- Bracketed weapon keywords (e.g. the IGNORES COVER / LETHAL HITS / DEVASTATING WOUNDS / etc. tags observed on weapon profiles throughout Sections 5 and 7) are formally defined Core Abilities with precise dice-mechanic rules each – Warscribe's tags are these ability names verbatim, not paraphrased UI labels.
- Duplicated Abilities: if a unit has the same core or weapon ability more than once (even with different numbers/keywords), the Core Rules require the controlling player to pick which instance applies. **Decided (project owner):** Warscribe leaves this to the player and displays both instances rather than offering a selection UI — Servitor matches this display-only behavior, no picker is built. See [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q2, resolved.

**Muster Armies: the real source of Build's DP, unit-limit, and enhancement rules (substantially corrects Section 4)**

> **Correction: enhancement uniqueness is not universal – "Upgrade"-tagged enhancements are a real exception**
> Section 4 stated "enhancements are unique per army" based on the observed "TAKEN" badge. The Core Rules show this is only true for ordinary enhancements. An enhancement tagged "Upgrade" is explicitly exempt: it can be given to non-CHARACTER units, and up to three copies of the same Upgrade are allowed in one army (paying its points cost each time; only the first copy counts against the overall enhancement limit). This exactly explains why the sample army files (Section 3.1) each show the same "(Upgrade)" enhancement applied to two different Boyz/Kommandos/Stormboyz units without conflict. E3.4's composition rules engine ([Backlog.md](Backlog.md)) special-cases Upgrade-tagged enhancements rather than applying a single uniqueness rule to all enhancements.

- Battle Size is an official, named, two-tier table on Wahapedia's Core Rules page: Incursion (1000 pts / 2 DP / 2 enhancement limit / 2 unit limit) and Strike Force (2000 pts / 3 DP / 4 enhancement limit / 3 unit limit). BSData additionally encodes a third tier, Onslaught, not present on this page (see Section 12 and [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q9, resolved — BSData is the authority for these values).
- The unit limit is doubled for Battleline and Dedicated Transport units, and every Epic Hero is capped at 1 regardless of battle size – confirmed directly in BSData's constraint structure (Q9, resolved).
- A detachment cannot be selected twice, and some detachments impose must-include/must-exclude constraints on other detachments or units in the list – a composition-rule type for E3.4.
- A Dedicated Transport with no embarked unit at the start of the battle is destroyed before play begins. Warscribe's Build validity check does **not** enforce this at list-building time (confirmed by re-test, see Section 12) — Servitor does not need to replicate it for V0.1.

---

## Section 12 — BSData Investigation & Warscribe Re-Test

Follow-up pass: inspected the BSData wh40k-11e catalogue files already in `/data/bsdata/wh40k-11e/` (the game-system file "Warhammer 40,000.json" and faction files "Imperium - Space Marines.json" / "Orks.json" / "Imperium - Astra Militarum.json"), and re-tested several previously-unconfirmed Warscribe behaviors. Findings below; corresponding items are marked resolved in [Open-Questions-Catalog.md](Open-Questions-Catalog.md).

**BSData contains all 3 official Battle Sizes, not 2**

> **Correction: Wahapedia's Core Rules table only showed 2 of 3 tiers**
> Section 11 cited Wahapedia's Muster Armies table as showing Incursion and Strike Force only. BSData's game-system file ("Warhammer 40,000.json") defines a third, explicit battle-size option not present in that table: Onslaught (3000-point limit).

| Battle Size | Points | DP | Enhancement limit |
|---|---|---|---|
| 1. Incursion | 1000 | 2 | 2 |
| 2. Strike Force | 2000 | 3 | 4 |
| 3. Onslaught | 3000 | 4 | 4 (shared with Strike Force – does not increase further) |

Values confirmed directly from the game-system JSON's roster-level constraints/modifiers (not inferred).

**The "Upgrade" enhancement distinction is structural, not a flag**
Comparing an Ork "Upgrade" enhancement ('Ardboyz) against a regular one (Mekwaaagh! Mastermind) in the Orks.json catalogue: both use the same BattleScribe entry type; the difference is entirely in their constraint: the regular enhancement caps at 1 copy at roster scope (matching the "TAKEN" badge behavior from Section 4), while the Upgrade enhancement caps at 3 copies at force scope. There is no dedicated "isUpgrade" field to read – Servitor's mapper reads each enhancement's own max-count constraint generically rather than hardcoding a single "enhancements are always unique" rule with a special case bolted on.

**Leader and Support are two separate, confirmed data relationships**
A bodyguard unit's constraints include two independent "associations" entries, one named "Leader" and one named "Support", each capped at 1 – directly matching the Core Rules (Section 11). An initial re-test pass found the UI-side attach flow titled "Attach Leader" on one bodyguard unit (Devastator Squad), with no separate Support-attach control observed there because that unit isn't eligible to take the Support character tested (Cato Sicarius) — inconclusive at the time. **Resolved in a later live session:** the "Attach Leader" modal lists both LEADER- and SUPPORT-tagged eligible characters together and both can be attached simultaneously — there is no separate "Attach Support" flow. See [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q1, resolved.

**Q9 resolved: the same-datasheet unit limit is a per-unit constraint + Incursion modifier, not a shared field**
Direct inspection of `Imperium - Astra Militarum - Library.json` resolved the mechanism precisely:
- Each unit carries its own `max` constraint (`field: selections`, `scope: force`) with a base value calibrated for Strike Force/Onslaught: `3` ordinary, `6` Battleline (Cadian Shock Troops, Catachan Jungle Fighters, Death Korps of Krieg all confirmed at 6).
- Epic Hero units instead use `scope: roster, max: 1` with no Incursion override — matches "capped at 1 regardless of battle size" exactly.
- A `modifiers` entry on the same unit conditionally sets that constraint's value to the Incursion figure (`2` ordinary / `4` Battleline) when the roster contains an "1. Incursion (1000 Point limit)" selection — verified on Bullgryn Squad (3→2 constraint id `4aa1-c327-c2d5-db67`) and Cadian Shock Troops (6→4).
- No separate Onslaught override exists anywhere — Onslaught silently shares the Strike Force base value, consistent with the DP/enhancement-limit sharing already confirmed above.

This is a fully generic, mapper-readable pattern: base constraint + optional Incursion-conditioned modifier on the same field id. No hardcoded per-battle-size table is needed or would even be correct, since BSData itself doesn't store one.

**Bigger finding: stratagem and mission rules text is not in BSData at all**
No stratagem entries (CP cost, WHEN/TARGET/EFFECT, Type) and no CP cost type were found anywhere in the game-system file or the sampled faction files – only occasional references to stratagems by name inside other abilities' description text (e.g. "… when you target this unit with the Crushing Impact stratagem …"). BSData, as included in this repo, is an army-building dataset (points, wargear options, unit stats, composition constraints), not a rules-reference dataset. It has no stratagem definitions and no mission/secondary-mission data.

> **Update: a second data source closes this gap for stratagems, abilities and enhancements**
> The project owner added `/data/core/` and `/data/factions/<faction>/faction.json` alongside BSData. `/data/core/core-stratagems.json` holds all 68 Core Stratagems; every detachment inside each faction's faction.json carries its own "stratagems", "abilities" (the detachment/Army Rule) and "enhancements" arrays. Inspection found 1,632 stratagem entries in total (68 core + 1,564 across 329 detachments in 24 factions), each with CP cost, full WHEN/TARGET/EFFECT/RESTRICTIONS text, Stratagem type (Battle Tactic/Strategic Ploy/Epic Deed/Wargear), and turn/phase scope. Enhancements likewise carry full legend + effect text and points cost. Text encoding was spot-checked (apostrophes etc.) and is correct UTF-8 – the mapping layer reads these files directly as JSON, stripping only the description field's inline `<b>`/`<span>` markup. This unblocks E6.2/E6.2b/E6.3.
>
> **Update (this session): mission content is now sourced separately, not from `/data/core/`**
> No mission-equivalent file exists in `/data/core/` — that part of the gap was closed instead via `wahapedia.ru`'s Mission Deck 2026-27 page directly (see [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q5/Q6, resolved). E5.1/E5.2 are unblocked.

**Overlap check: the two data sources do not replace each other**
Explicitly compared datasheet content between BSData and `/data/factions/<faction>/datasheets/` to check whether BSData could be dropped now that the second source is confirmed. Both sources carry points costs, model/weapon stat lines, keywords and unit composition, so there is real overlap at the datasheet level. But BSData's wargear-option constraints are structured data (selection entries with min/max counts, force/roster-scope enhancement uniqueness, the Battle-Size-conditioned DP/enhancement/unit limits) – exactly what Build's (E3) validation engine and wargear-config UI (single-slot radio vs. quantity stepper, Section 4) read programmatically. The second source's equivalent field (a datasheet's "options" array, e.g. Tactical Squad's weapon-swap options) is free-text English prose inside an HTML description ("1 Tactical Marine's boltgun can be replaced with one of the following: …"), with no structured min/max count anywhere in the entry – turning that into enforceable build logic would mean parsing natural language, not reading a field. **Conclusion:** keep both sources, with a clear split – BSData (E1.3) feeds Build's validation/constraint logic, the rules-text source (E1.3b) feeds Scry/Action Queue display content. Neither is redundant.

**The two sources disagree on Battle Sizes – BSData is authoritative, Onslaught stays in scope**
`/data/core/muster-armies.json`'s "Select Battle Size" entry contains the same HTML table Wahapedia's own page shows: only 2 tiers (Incursion, Strike Force). It does not mention Onslaught at all. BSData's game-system JSON has all 3 tiers including Onslaught (3000 points, 4 DP, 4 enhancements). Per the project owner's explicit decision, Onslaught is a real, currently-played variant and stays in scope for E3.1/E3.3 regardless of this gap in the rules-text source. Practical consequence for the mapping layers: Battle Size/DP/enhancement-limit values are always sourced from BSData (E1.3), not from the rules-text source's muster-armies chapter (E1.3b is for stratagem/ability/enhancement content, not composition limits) – the two sources must not be silently merged or cross-checked against each other for this specific data point.

**Warscribe re-test results**
- **Empty Dedicated Transport**: adding a Rhino with no unit assigned to embark did **not** flip Build's validity indicator to invalid – Warscribe does not enforce the "empty transport is destroyed" rule at list-building time.
- **Mandatory Support attachment is enforced**: adding a Support-only character (Cato Sicarius) without a bodyguard immediately produced a real validation error ("Cato Sicarius must be attached to a bodyguard unit") and flipped the indicator to "1 Issue" – confirms Build's composition engine does enforce at least this specific Core Rules requirement, not just points/DP.
- **"Add / Manage detachments"** opens a grid of all detachments available to the selected faction, each showing its own DP cost, with detachments that would exceed the remaining DP budget shown disabled.
- **"Compact"** toggles the unit-card rendering between a one-line (name + points) summary and the full card (wargear list, Wargear/Enhance/Leader buttons); the button's own label always names the mode you would switch to.
- **The Wargear modal's "STATS" control** expands the weapon stat-block inline within the same modal – it does not navigate to the Scry-style datasheet page.
- **A Scoring "how to score" condition** with a "for each X" cumulative shape is a −/count/+ stepper, not a checkbox; incrementing it updates the round total, Primary VP total, and composite score instantly, regardless of the current Play phase. A boolean end-of-battle-only condition on the same mission genuinely stayed disabled until the relevant round — [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q3, resolved.
- **"CHALL"** on the Scoring header is a manually-adjusted counter with its own +/− stepper, adding directly to the composite score; identified as the Challenger-card catch-up mechanic from the 2025-26 Chapter Approved Mission Deck — [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q4, resolved.
- **Support attachment** uses the same "Attach Leader" modal as Leader attachment, listing both role types together; a Leader and a Support can be attached to the same unit simultaneously — [Open-Questions-Catalog.md](Open-Questions-Catalog.md) Q1, resolved.
- **A second disposition-pair spot-check** (Disruption + Reconnaissance → Smoke and Mirrors) matched the table inferred from the Mission Deck page exactly, and the Scoring screen's Deployment & Layout dropdown for that pair listed 3 of the Mission Deck's 6 named Deployment cards as Layout A/B/C, cross-confirming decision D13.

This document is a living reference; re-verify a section against the live app before implementing it if the reference app has likely changed since 2026-09-17. Rule citations above reference wahapedia.ru's Core Rules and Mission Deck pages as retrieved on the same date; Games Workshop errata/FAQ updates can change specifics without changing the overall structure described here.
