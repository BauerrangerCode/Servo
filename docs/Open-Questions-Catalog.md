# Open Questions Catalog

Servitor V0.1 – v6, closed out: Q2 decided by the project owner, Q7 dropped as out of scope

v1 of this catalog (Core Rules cross-reference only) listed 21 questions in four categories. v2 reflected a follow-up pass that inspected the BSData wh40k-11e catalogue files, re-tested several Warscribe behaviors live, and recorded two project-owner decisions. v3 reflected the discovery of a second local rules-data source (`/data/core/`, `/data/factions/`) covering stratagems/abilities/enhancements. v4 resolved Q9 by direct BSData inspection, resolved Q5/Q6 via the Wahapedia Mission Deck 2026-27 page, and recorded three project-owner decisions (D11–D13). v5 reflected a live Warscribe re-test session resolving Q1, Q3, Q4. This update (v6) closes the catalog out:

- **Q2** decided by the project owner: Warscribe leaves Duplicated Abilities to the player — it displays both instances and does not offer a selection UI. Servitor matches this (display-only, no forced choice).
- **Q7** (Painting bonus format/event-dependence) dropped: the project owner ruled this out of scope for Servitor entirely. Every mention has been removed from this catalog and from [Reference-App-Analysis.md](Reference-App-Analysis.md); see the Out-of-Scope note at the end of this document.

All originally-tracked questions are now resolved or explicitly out of scope. No open items remain.

## Contents

- R. Resolved / decided (23 items)
- D. Scope/sequencing decisions for the project owner (0 open — all decided)
- Out of Scope (1 item: Painting bonus configurability)

---

## CATEGORY R — Resolved / Decided

### ✓ All 3 official Battle Sizes exist, not 2 (Incursion/Strike Force/Onslaught)

Wahapedia's Core Rules table only showed 2 tiers. BSData's game-system file explicitly defines a third: Onslaught (3000 points, 4 DP, 4 enhancement limit – shared with Strike Force). Per the project owner's decision (Category D), all three are in scope for E3.1/E3.3, not just Strike Force. See [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12 for the full table.

### ✓ Decision: Servitor tracks real stratagem/ability usage state, not reference-only

The project owner confirmed Servitor should enforce the Core Rules' real usage limits (same stratagem not twice per phase; a unit not targeted by more than one stratagem per phase) rather than matching Warscribe's apparent reference-only behavior. Story E6.2b in [Backlog.md](Backlog.md).

### ✓ The "Upgrade" enhancement distinction is structural in BSData, not a dedicated flag

Confirmed directly in `Orks.json`: a regular enhancement caps at 1 copy at roster scope; an "Upgrade" enhancement caps at 3 copies at force scope. Same BattleScribe entry type for both – the distinction lives entirely in each entry's own constraint, which Servitor's mapper should read generically rather than hardcoding a single "enhancements are unique" rule.

### ✓ BSData does encode Leader and Support as two separate roles

A bodyguard unit's data includes two independent "associations" constraints, named "Leader" and "Support", each capped at 1 – confirms the Core Rules distinction ([Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11) is present in the actual data Servitor will consume.

### ✓ BSData carries Detachment Points (DP) limits at the game-system level

The three Battle-Size-conditioned DP values (2/3/4) are set directly in the shared game-system file's roster-level constraints – confirmed by inspection, not assumed.

### ✓ Warscribe does NOT flag an empty Dedicated Transport as invalid

Added a Rhino with no unit assigned to embark; Build's validity indicator stayed "VALID". Servitor does not need to replicate the "empty transport is destroyed" rule to match the reference app for V0.1.

### ✓ Warscribe DOES enforce mandatory Support-unit attachment

Adding a Support-only character (Cato Sicarius) without a bodyguard immediately produced a real validation error and flipped the indicator to "1 Issue" – confirms Build's composition engine enforces at least this one Core Rules requirement beyond points/DP.

### ✓ "Add / Manage detachments" is a DP-gated grid, not a multi-step wizard

Opens a grid of every detachment available to the faction, each tagged with its own DP cost; options that would exceed the remaining DP budget are shown disabled.

### ✓ "Compact" toggles card density; the Wargear modal's "STATS" button expands inline

Compact switches unit cards between a one-line (name + points) summary and the full card. STATS expands the weapon stat-block inside the same Wargear modal – it does not navigate to the Scry-style datasheet page.

### ✓ A rules-text data source for stratagems, detachment abilities and enhancements exists

`/data/core/` and `/data/factions/<faction>/faction.json` (added by the project owner) carry the full stratagem/ability/enhancement rules text BSData lacks – 1,632 stratagem entries total. Unblocks E6.2/E6.2b/E6.3. See [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 12.

### ✓ Decision: keep both data sources – they overlap but neither replaces the other

BSData (E1.3) stays as the source for Build's validation/constraint logic (structured wargear-option limits); the rules-text source (E1.3b) is used for Scry/Action Queue rules-text content (free-text prose, not usable for programmatic build validation).

### ✓ Onslaught (3000pts) stays in scope; BSData is the authority for Battle Size data specifically

The rules-text source's muster-armies chapter only shows 2 tiers (no Onslaught); BSData's game-system file has all 3. Project owner decision: Onslaught is a real, currently-played variant and must be kept. Battle Size/DP/enhancement-limit values are always read from BSData, never from the rules-text source.

### ✓ Q9 — BSData's same-datasheet unit limit is a per-unit constraint + Incursion modifier, not one shared field

Resolved by direct inspection of `data/bsdata/wh40k-11e/Imperium - Astra Militarum - Library.json` (pattern generalizes across factions):

- No generic/shared field exists at game-system level for "max copies of the same datasheet".
- Each unit carries its own `max` constraint (`field: selections`, `scope: force`), with a base value calibrated for **Strike Force/Onslaught**: `3` for ordinary units, `6` for **BATTLELINE** units (exactly doubled).
- **EPIC HERO** units instead use `scope: roster, max: 1` — no Incursion override exists, matching "capped at 1 regardless of battle size".
- For non-Epic-Hero units, a `modifiers` entry on the same unit conditionally overrides that constraint's value to the **Incursion** value (`2` ordinary / `4` Battleline) when the roster contains at least one "1. Incursion (1000 Point limit)" selection. Verified on Bullgryn Squad (3→2) and Cadian Shock Troops (6→4).
- Onslaught has no separate override anywhere — it uses the same base value as Strike Force, consistent with the already-confirmed DP/enhancement-limit sharing.

**Consequence for Servitor's mapper (E3.4):** read each unit's base force/roster-scope `max` constraint, then check for a modifier targeting the same constraint field conditioned on an "Incursion" roster selection to get the Incursion-tier override. No central lookup table needed or possible.

### ✓ Q5 — Full disposition-pair → Primary Mission mapping, with exact scoring values

Resolved via `wahapedia.ru/wh40k11ed/the-rules/mission-deck-2026-27/` (read in full, 2026-09-17). All 25 Primary Missions (5 Force Dispositions × 5 possible opponent dispositions) are named with complete, structured "How to score" conditions (WHEN / condition / VP value, including cumulative and end-of-battle bonuses) — not flavour text.

The disposition-pair → mission table (player's own disposition as row, opponent's disposition as column, in the order Take and Hold / Purge the Foe / Disruption / Reconnaissance / Priority Assets):

| Your Disposition ↓ | vs Take and Hold | vs Purge the Foe | vs Disruption | vs Reconnaissance | vs Priority Assets |
|---|---|---|---|---|---|
| **Take and Hold** | Battlefield Dominance | Immovable Object | Determined Acquisition | Purge and Secure | Inescapable Dominion |
| **Purge the Foe** | Unstoppable Force | Meatgrinder | Punishment | Consecrate | Destroyer's Wrath |
| **Disruption** | Death Trap | Delaying Action | Outmanoeuvre | Smoke and Mirrors | Locate and Deny |
| **Reconnaissance** | Reconnaissance Sweep | Triangulation | Surveil the Foe | Gather Intel | Search and Scour |
| **Priority Assets** | Secure Asset | Vital Link | Extract Relic | Vanguard Operation | Sabotage |

Confidence note: the page's text extraction lists each Force Disposition card's 5 missions in a fixed order but the opponent-symbol-to-column mapping is graphical on the physical card, not separately labelled text. The table above is inferred from that fixed order plus one directly-confirmed data point (Take and Hold + Purge the Foe → Immovable Object, confirmed live in Warscribe earlier) and is internally consistent (matches the mission generator's dropdown grouping exactly). Treat as high-confidence but not pixel-verified against the card art; spot-check one or two more combinations against the live Warscribe app if a mismatch is ever suspected.

Unblocks E5.1. Source content is versioned ("Mission Deck 2026-27" — a seasonal Chapter Approved pack); re-check against the live page if a future season changes mission content.

### ✓ Q6 — Full Fixed-secondary-mission list and Tactical deck contents

Resolved via the same Wahapedia page: the complete Secondary Mission deck (over 20 named cards, e.g. No Prisoners, Overwhelming Force, Plunder, Display of Might, Outflank, Beacon, Cleanse, A Grievous Blow, Defend Stronghold, Engage on All Fronts, Secure No Man's Land, Forward Position, Centre Ground, Assassination, A Tempting Target, Behind Enemy Lines, Bring It Down, Burden of Trust, etc.) with full WHEN/condition/VP text. Several cards show distinct **FIXED** vs **TACTICAL** VP values for the same condition (e.g. A Grievous Blow: 5VP Fixed / up to 5VP Tactical); others show one flat value usable in either mode. Unblocks E5.2.

### ✓ D11 — Decision: Upgrade-enhancement exception ships together with the ordinary rule in V0.1

Project owner confirmed both the ordinary "unique per army" rule and the Upgrade exception ship together in E3.4 — not staged. The BSData mechanism for both is already generic and simple to implement (see Q9's resolution pattern), so there is no meaningful cost reduction from splitting them.

### ✓ D12 — Decision: per-model-role wound tracking is in scope for V0.1

Project owner confirmed E6.6 should track wounds per model-role (leader / bodyguard / wargear-bearer) within an attached/combined unit card, not just one aggregate wound count. This is needed to correctly reproduce attached-unit ability persistence after a specific model's death (see [Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11). E6.6's acceptance criteria and point estimate need updating in the backlog to reflect this (see [Backlog.md](Backlog.md)).

### ✓ Q2 — Decision: Duplicated Abilities are left to the player, both instances are displayed

The Core Rules require the player to pick which instance applies when a unit has the same ability twice with different values (e.g. two different Scout ranges). A live re-test session found no concrete duplicate-ability example to observe directly, so the project owner decided the behavior outright: Warscribe leaves this to the player and displays both instances rather than offering a selection UI. Servitor matches this — the datasheet/Action Queue rendering (E2.3/E6.2) shows all instances of a duplicated ability as-is; no picker/selection interaction is built for this case.
*[Reference-App-Analysis.md](Reference-App-Analysis.md) Section 11*

---

## CATEGORY A — Re-test Against the Live App (all resolved)

### ✓ Q1 — Support attachment is the exact same UI flow as Leader attachment

Resolved by a live rebuild session (Space Marines, Blade of Ultramar, Victrix Honour Guard + Cato Sicarius + Captain Titus). Victrix Honour Guard's own datasheet states it accepts a CAPTAIN or CHAPTER MASTER as attached Leader; Cato Sicarius's datasheet states he (tagged SUPPORT) can attach specifically to Victrix Honour Guard. Opening Victrix Honour Guard's single "LEADER" button opens a modal titled **"Attach Leader · Victrix Honour Guard"** that lists *both* Cato Sicarius (badge: SUPPORT) and Captain Titus (badge: LEADER) side by side, each with its own independent Attach button. Attaching both in sequence succeeded simultaneously — the unit card then read "Led by Cato Sicarius, Captain Titus". **There is no separate "Attach Support" flow or control anywhere** — Leader and Support are two independently-attachable roles surfaced through one shared modal/action, which lists eligible characters of either role and tags each with its role badge.
*[Reference-App-Analysis.md](Reference-App-Analysis.md) Section 4/12*

### ✓ Q3 — Yes, a Primary VP scoring condition updates totals immediately; end-of-battle-only conditions are correctly gated

Resolved by live testing on the Scoring screen (Smoke and Mirrors mission, Round 1). "Any battle round" conditions are rendered as a `−`/count/`+` stepper (not a plain checkbox — refines the earlier observation), and clicking `+` updated the Round 1 subtotal, the Primary VP total, and the top-level composite score (`10` → `12`, breakdown `2+0+10`) **instantly**, with no phase gating. The "End of Battle"-only condition on the same mission ("Four or more objectives are decoyed") is rendered as a checkbox that stayed genuinely disabled/unclickable in Round 1, confirming end-of-battle-only conditions *are* correctly gated by round context, while any-battle-round conditions are not — the earlier session's "no visible change" was a missed click, not a real timing gate.
*[Reference-App-Analysis.md](Reference-App-Analysis.md) Section 6/12*

### ✓ Q4 — Tactical secondary cards match the Mission Deck 1:1; CHALL is the "Challenger" catch-up mechanic

Resolved by live testing. Drawing 2 Tactical secondary cards produced "Plunder" and "Assassination" with the exact WHEN/VP/ACTION structure documented on the Wahapedia Mission Deck page (Q6) — independent confirmation that Warscribe's Tactical deck is the same content. Separately, incrementing the "CHALL" stepper (`+1`) added directly to the player's total score as its own 4th composite term (`2+0+10` → `2+0+10+1`) — a manually-entered value, not board-state-derived. Web research (Bell of Lost Souls, Flipside Gaming, Tabletop Battles "Hammer of Math") identifies this as the **Challenger card** catch-up mechanic introduced in the 2025-26 Chapter Approved Mission Deck: a player trailing by 6+ VP draws a Challenger card each Command phase, granting either a free Stratagem or a bonus scoring option (~3 VP, rules sources differ slightly on the exact cap). Warscribe cannot detect the VP gap or which option was chosen automatically, so it exposes CHALL as a manual counter the player adjusts themselves.
*[Reference-App-Analysis.md](Reference-App-Analysis.md) Section 6/12*

### ✓ Additional confirmation for Q5's disposition-mapping table

Spot-checked a second combination live: **Disruption (yours) + Reconnaissance (enemy) → "Smoke and Mirrors"**, exactly matching the table inferred in Q5's resolution. Two of 25 combinations are now directly confirmed live (the other being Take and Hold + Purge the Foe → Immovable Object), both matching the inferred table exactly — confidence raised from "high, not pixel-verified" to "confirmed by two independent live spot-checks, consistent with the full table's derivation logic". The Scoring screen's "Deployment & Layout" dropdown for this pair also listed exactly 3 of the Mission Deck's 6 named Deployment cards (Tipping Point, Search and Destroy, Dawn of War) as Layout A/B/C — direct confirmation that D13's Deployment Card content lines up with Warscribe's own curated layout options.

---

## OUT OF SCOPE

### Painting-bonus configurability

The Painting bonus (observed as a flat 10 VP for a fully painted army in one live game) was originally an open question about whether that value is fixed by core matched-play rules or configurable per event/format. The project owner ruled this **not relevant to Servitor** and explicitly out of scope — no investigation, no configuration option, no format-dependent handling. E5.2's "Paint" score component stays a flat, non-configurable value. This item is not tracked as an open question; it is intentionally not implemented further.

---

## CATEGORY D — Scope & Sequencing Decisions for the Project Owner

*(D11, D12 and D13 are resolved — see Category R.)*

### ✓ D13 — Decision: Servitor uses the Deployment Card content as a real deployment/layout source

Project owner confirmed Servitor should consume the Mission Deck's 6 named Deployment cards (Tipping Point, Sweeping Engagement, Search and Destroy, Hammer and Anvil, Dawn of War, Crucible of Battle) as E5.1's curated named-layout data — each card's structured rules (battlefield edges, deployment zones, objective-point placement) are read directly, not guessed or left as a bare link. The third-party visual editor (AlpacaSoft) remains out of scope; this decision is only about consuming the textual layout data, not building a visual battlefield editor. [Backlog.md](Backlog.md) E5.1 and the EPIC – Traceability section are updated accordingly.

---

Source: [Reference-App-Analysis.md](Reference-App-Analysis.md) (Sections 4, 6, 7, 11, 12) cross-referenced against wahapedia.ru's Core Rules and Mission Deck 2026-27 pages, the BSData wh40k-11e catalogue files in `/data/bsdata/wh40k-11e/`, the stratagem/ability/enhancement data in `/data/core/` and `/data/factions/`, and a live re-test session against `40k.warscribe.app` (2026-09-17). This catalog is closed: every tracked question is resolved, decided, or explicitly out of scope.
