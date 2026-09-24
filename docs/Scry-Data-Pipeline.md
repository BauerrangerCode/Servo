# Scry Data Pipeline — Detailed Walkthrough

This document explains, step by step, everything that happens between "user opens `/scry`" and "a datasheet
row appears on screen" — and documents a **confirmed root cause** for the missing-datasheet problem reported
against the Orks catalogue (18 of 67 Ork datasheets shown).

> **Update:** the bug described in §5–§7 has since been fixed in `BsDataCatalogueMapper.cs`, following a
> subsequent code review (`docs/Code-Review-2026-09-18.md`) that flagged it as unfixed. Sections below are kept
> in their original investigative wording (including "currently dropped" / "no code has been changed") since
> they were written and reviewed before the fix — treat this note as the current status, and §11 (added below)
> as the fix summary. Live-verified in the browser: Orks went from 18/67 to 63/67 non-Legends datasheets shown.

> **Superseded (loading flow):** Scry no longer fetches or maps the raw BSData files itself. `BsDataCatalogueManifest`,
> the per-file `HttpClient` loading, the per-file IndexedDB cache and the loading flow in §1–§3 were replaced by the merged
> single source of truth, see §12. §4–§7 (structure of the raw BSData JSON, the mapping bug and its measurements) and §8–§9
> (search, rendering) remain accurate; file paths in them are the pre-merge ones (the mappers, including
> `BsDataCatalogueMapper`, now live in `src/Servitor.RulesData/Mapping/`).

---

## 1. High-level flow

```
Browser opens /scry
        │
        ▼
Scry.razor : OnInitializedAsync()
        │
        ▼
DatasheetCatalog.GetAllAsync()                 (RulesData/Loading/DatasheetCatalog.cs)
        │
        │  loops over BsDataCatalogueManifest.CatalogueFileNames (45 file names)
        ▼
per file: LoadCatalogueAsync(fileName)
        │
        ├─ IndexedDB has a cached mapping for (fileName, DataVersion)? ──yes──▶ use cached Datasheets
        │
        └─ no ──▶ HttpClient fetches the raw JSON from
                  wwwroot/data/bsdata/wh40k-11e/<fileName>
                        │
                        ▼
                  BsDataCatalogueMapper.Map(json)        (RulesData/Mapping/BsDataCatalogueMapper.cs)
                        │
                        │  parses BattleScribe/BSData JSON → List<Datasheet> (domain model)
                        ▼
                  result saved to IndexedDB, keyed by "<fileName>::<DataVersion>"
        │
        ▼
all 45 files' Datasheets concatenated into one in-memory List<Datasheet>
        │
        ▼
Scry.razor keeps this list in `_allDatasheets` for the rest of the page's lifetime (never re-fetched)
        │
        ▼
DatasheetSearchService.Search(_allDatasheets, criteria)  (RulesData/Search/DatasheetSearchService.cs)
        │
        │  pure in-memory filtering: text query, role chips, faction chips, legends toggle, characteristic filter
        ▼
`_filtered` (List<Datasheet>) is rendered in a virtualized <table> in Scry.razor
        │
        ▼
clicking a row sets `_selected` → <DatasheetDetail Datasheet="_selected" /> renders the full statline/weapons/abilities
```

Everything from "fetch" to "map" to "cache" happens **once per browser profile** (until the cached
`DataVersion` changes) — after the first successful load, all 45 files come straight out of IndexedDB and no
network request happens again.

---

## 2. Step 1 — The file manifest and cache key

**File:** `RulesData/Loading/BsDataCatalogueManifest.cs`

```csharp
public const string DataVersion = "8f5b01a";
public static readonly IReadOnlyList<string> CatalogueFileNames = [ /* 45 hardcoded file names */ ];
```

- `DataVersion` is a fixed string — the short git commit hash of the BSData source repository at the time
  the JSON files were copied into `wwwroot/data/bsdata/wh40k-11e/`. It is **not** derived from the files
  themselves; it's a manually-set constant.
- `CatalogueFileNames` is a **hardcoded list of 45 filenames**. One real file that exists on disk,
  `"Warhammer 40,000.json"` (the game-system file), is deliberately excluded because its JSON root key is
  `"gameSystem"`, not `"catalogue"` — the mapper can never parse it.
- **Implication:** if BSData adds/renames/splits a catalogue file upstream, this list must be updated by
  hand. It is not auto-discovered from the folder contents.

---

## 3. Step 2 — Fetch-or-cache per file

**File:** `RulesData/Loading/DatasheetCatalog.cs`

```csharp
private async Task<IReadOnlyList<Datasheet>> LoadCatalogueAsync(string fileName, CancellationToken ct)
{
    var stored = await store.LoadFactionAsync(fileName, BsDataCatalogueManifest.DataVersion, ct);
    if (stored is not null) return stored.Datasheets;          // ← IndexedDB cache hit

    var url = $"data/bsdata/wh40k-11e/{Uri.EscapeDataString(fileName)}";
    var json = await httpClient.GetStringAsync(url, ct);       // ← cache miss: fetch raw JSON
    var result = BsDataCatalogueMapper.Map(json);               // ← parse + map

    await store.SaveFactionAsync(new FactionRulesData(fileName, DataVersion, result.Datasheets, [], [], []), ct);
    return result.Datasheets;
}
```

Key points:

- The cache key is `fileName + "::" + DataVersion` (built in `IndexedDbRulesDataStore.BuildKey`). Since
  `DataVersion` never changes automatically, **once a faction file is successfully mapped and cached, it is
  never re-mapped again** — even if the mapper's logic changes later (this is exactly why the earlier
  stat-line bug fix required manually deleting the IndexedDB database during testing).
- `result.Warnings` from the mapper (see below) are **silently discarded here** — `LoadCatalogueAsync` never
  looks at `result.Warnings`, and `FactionRulesData` has no field for them. If the mapper skips a unit with a
  warning, there is currently no way for the UI or the user to see that a unit was skipped.
- Caching is per-file, not per-datasheet — one bad/incomplete mapping for a file poisons the cache for that
  entire file until `DataVersion` is bumped or IndexedDB is cleared manually.

---

## 4. Step 3 — What the raw BSData JSON actually looks like

Each catalogue file's top-level shape (verified directly against `Orks.json`) is:

```
catalogue
├── name, id, authorName, revision, ...          (metadata)
├── catalogueLinks[]                              (references to OTHER catalogue files, e.g. shared rule sets)
├── categoryEntries[]                              (role/keyword category definitions, e.g. "Battleline")
├── sharedRules[], rules[]                         (core rules text — not used by the Datasheet mapper)
├── sharedProfiles[]                               (a pool of reusable stat blocks, referenced by id)
├── profileTypes[]                                 (schema metadata for profile characteristics)
├── sharedSelectionEntries[]  ← THE DATASHEETS      (flat top-level array — see below)
├── sharedSelectionEntryGroups[]                    (grouping nodes: detachments, enhancement lists, etc.)
└── entryLinks[]  ← TOP-LEVEL, catalogue-wide        (see §6 — this is the part that matters for the bug)
```

The datasheet-relevant content lives almost entirely in `sharedSelectionEntries[]`. Each element in that
array has a `"type"` field. In `Orks.json`, a **direct inspection of every element's `type`** shows:

| top-level `sharedSelectionEntries[]` entry type | count in Orks.json | meaning |
|---|---|---|
| `"unit"` | 21 | multi-model squad datasheets (e.g. *Boyz*, *Nobz*, *Gretchin*) |
| `"model"` | 57 | **single-model datasheets** (every Character, Vehicle, Walker, Monster — e.g. *Ghazghkull Thraka*, *Gorkanaut*, *Deff Dread*, *Dakkajet*, *Wazdakka Gutsmek*, *Big Mek in Mega Armour*, …) |

Both `"unit"` and `"model"` entries sit **at the exact same structural depth**, as direct siblings in the same
array. BSData/BattleScribe's convention is: a squad that can contain multiple models of different profiles is
a `"unit"`; a datasheet that is inherently a single model (most Characters, all Vehicles/Walkers/Monsters,
Dedicated Transports) is a `"model"`. Both are equally valid, playable datasheets.

---

## 5. Step 4 — How `BsDataCatalogueMapper` builds the index

**File:** `RulesData/Mapping/BsDataCatalogueMapper.cs`

```csharp
var entriesById = new Dictionary<string, BsEntry>();
foreach (var entry in catalogue.SharedSelectionEntries)
{
    IndexEntries(entry, entriesById);          // recursively walks EVERY entry, of ANY type, into one flat dict
}
```

`IndexEntries` (lines 69–90) recurses into `entry.EntryLinks`, `entry.SelectionEntryGroups`, and
`entry.SelectionEntries` of every node, indexing every entry it finds **by id, regardless of `Type`**. So at
this point, `entriesById` already contains all 21 `"unit"` entries **and** all 57 `"model"` entries from
`Orks.json` — nothing is lost yet.

The loss happens immediately after, in `Map()` itself (line 54):

```csharp
foreach (var entry in entriesById.Values.Where(e => e.Type == "unit"))   // ← only "unit"!
{
    datasheets.Add(MapUnit(entry, ...));
}
```

**This is the confirmed root cause.** Every entry with `Type == "model"` is present in `entriesById`, fully
indexed and reachable, but is **never passed to `MapUnit`, never becomes a `Datasheet`, and produces no
warning** — it is silently invisible to the rest of the app. There is no bug in the recursive indexing, no bug
in JSON deserialization, and no missing file — it is a single filter predicate that only recognizes one of the
two valid datasheet shapes.

### Why this matches "18 of 67 shown" for Orks

- Orks.json has 21 `"unit"` + 57 `"model"` = 78 standalone datasheets structurally present in the file.
- The mapper currently only produces the 21 `"unit"` ones.
- Of those 21, some are marked `[Legends]` in their name and are hidden by Scry's default "Current" (non-Legends)
  view (see §8) — leaving the ~18 the user counted.
- 67 is the reference app's (Warscribe) count for the same faction; the gap between the mapper's 21/78 and
  Warscribe's 67 is explained almost entirely by the dropped `"model"` entries, not by a fundamentally
  different data source.

### `MapUnit` / `CollectFromSubtree` — how a matched entry becomes a `Datasheet`

For every entry that *does* reach `MapUnit` (i.e., every `"unit"`-typed entry today), the mapper recursively
walks that entry's own subtree (`CollectFromSubtree`, lines 109–166) to collect:

- **Stat line** — first `Profiles` entry with `TypeName == "Unit"` found anywhere in the subtree, checking
  both the profile being inlined directly on a node (`node.Profiles`) and the profile being referenced
  indirectly via `node.InfoLinks` → `sharedProfiles` pool (this two-shape handling was added as part of the
  E2 mapping-bug fix, see the `Aeldari - Aeldari Library.json` "Dark Reapers" regression test).
- **Ranged/melee weapons** — every `Profiles` entry with `TypeName == "Ranged Weapons"` / `"Melee Weapons"`
  anywhere in the subtree, deduplicated by weapon name (`target.All(w => w.Name != weapon.Name)`).
- **Wargear abilities** — every `Profiles` entry with `TypeName == "Abilities"` found on a *child* node
  (`isUnitItself: false`); abilities on the unit's own top-level `Profiles` become **Named** abilities instead
  (collected separately, right after `CollectFromSubtree` returns).
- **Core abilities** — synthesized (not read from a profile) by checking `CategoryLinks` for `"Leader"` /
  `"Support"` tags.
- **Composition constraint** — the base `max`-type constraint (`field: "selections"`, `scope: "force"` or
  `"roster"`) plus an optional Incursion-battle-size override read from `Modifiers` (Q9 logic).
- A `visited` `HashSet<string>` guards against infinite recursion if the entry graph contains cycles (unlikely
  but structurally possible via `entryLinks`).

None of this logic is affected by the `"model"` bug — it's entirely about *what happens once an entry is
accepted*. The bug is strictly at the *which entries are accepted* step.

---

## 6. The catalogue's top-level `entryLinks[]` — a second, related gap

Separately from `sharedSelectionEntries[]`, every catalogue file also has a **top-level** `entryLinks[]` array
(76 entries in `Orks.json`) that is **never read by the mapper at all** (`BsCatalogue` in
`BsData/BsCatalogueDocument.cs` only deserializes `name`, `sharedSelectionEntries`, `sharedProfiles` — it has
no property for the catalogue-root `entryLinks[]` or `sharedSelectionEntryGroups[]`).

Investigation showed these top-level `entryLinks` are **not** the cause of the missing datasheets — each one
has a `targetId` that resolves to an entry that *is* already reachable inside `sharedSelectionEntries[]` (the
21 `"unit"` + 57 `"model"` entries above are exactly what these links point at). Functionally, this top-level
array appears to represent the default army-list/"roster" selection order/visibility, not additional data. It
is flagged here for completeness because it is genuinely unused, not because it explains any of the missing
data.

`sharedSelectionEntryGroups[]` (12 entries in `Orks.json`, e.g. "Detachment", "Enhancements") is also never
indexed. A direct comparison of reachable `"unit"`-typed ids with vs. without indexing this array found **zero
additional units** — every unit-relevant entry was already reachable through `sharedSelectionEntries[]`. This
array holds detachment/enhancement/stratagem selection structure, not extra datasheets, and is out of scope
for Scry (E2) regardless.

---

## 7. Scope of the bug — measured across all 45 manifest files

Counting `"type": "unit"` vs. `"type": "model"` **only among direct/top-level `sharedSelectionEntries[]`
items** (i.e., real standalone datasheets, not nested wargear/loadout sub-entries inside a squad — see the
caveat in §7.1) across every file in the manifest:

| Category | Count |
|---|---|
| Top-level `"unit"` entries (currently mapped) | **466** |
| Top-level `"model"` entries (currently **dropped**) | **1,053** |
| Total standalone datasheets structurally present | **1,519** |

(The 466 here vs. the "468" figure quoted in the earlier E2 status update is a minor discrepancy from a small
number of `"unit"`-typed entries that live one level deeper than direct children of `sharedSelectionEntries`
in a couple of files — not relevant to this bug, not investigated further here.)

Per-file breakdown (files with at least one dropped `"model"` datasheet):

| File | unit (mapped) | model (dropped) |
|---|---:|---:|
| Aeldari - Aeldari Library.json | 48 | 86 |
| Chaos - Chaos Daemons Library.json | 19 | 52 |
| Chaos - Chaos Knights Library.json | 0 | 20 |
| Chaos - Chaos Space Marines.json | 34 | 50 |
| Chaos - Death Guard.json | 15 | 35 |
| Chaos - Emperor's Children.json | 9 | 17 |
| Chaos - Thousand Sons.json | 12 | 29 |
| Chaos - World Eaters.json | 10 | 26 |
| Genestealer Cults.json | 8 | 20 |
| Imperium - Adepta Sororitas.json | 19 | 27 |
| Imperium - Adeptus Custodes.json | 12 | 22 |
| Imperium - Adeptus Mechanicus.json | 22 | 19 |
| Imperium - Agents of the Imperium.json | 22 | 33 |
| Imperium - Astra Militarum - Library.json | 36 | 106 |
| Imperium - Black Templars.json | 6 | 14 |
| Imperium - Blood Angels.json | 7 | 20 |
| Imperium - Dark Angels.json | 6 | 13 |
| Imperium - Deathwatch.json | 8 | 8 |
| Imperium - Grey Knights.json | 7 | 27 |
| Imperium - Imperial Fists.json | 0 | 3 |
| Imperium - Imperial Knights - Library.json | 1 | 21 |
| Imperium - Iron Hands.json | 0 | 2 |
| Imperium - Raven Guard.json | 0 | 2 |
| Imperium - Salamanders.json | 0 | 2 |
| Imperium - Space Marines.json | 41 | 91 |
| Imperium - Space Wolves.json | 15 | 35 |
| Imperium - Ultramarines.json | 5 | 11 |
| Imperium - White Scars.json | 0 | 2 |
| Leagues of Votann.json | 12 | 13 |
| Library - Astartes Heresy Legends.json | 0 | 37 |
| Library - Titans.json | 0 | 4 |
| Library - Tyranids.json | 6 | 13 |
| Necrons.json | 20 | 47 |
| **Orks.json** | **21** | **57** |
| T'au Empire.json | 24 | 42 |
| Tyranids.json | 20 | 25 |
| Unaligned Forces.json | 1 | 22 |

Every single faction is affected — this is not an Orks-specific issue, it just happened to be the one the
user spot-checked.

### 7.1 Caveat: not all nested `"model"` entries are datasheets

`"model"`-typed entries can also appear **nested inside** a `"unit"`-typed entry's own subtree (representing
selectable loadout variants for individual models within a squad, not a separate datasheet). Those nested
occurrences must **not** be turned into separate top-level `Datasheet` records — doing so would overcount. The
1,053 figure above counts **only** `"model"` entries that are direct children of `sharedSelectionEntries[]`
(i.e., structural siblings of `"unit"` entries), which is the correct scope for "missing standalone
datasheets." A full unrestricted recursive count of `"model"` anywhere in the tree is much higher (2,502) but
includes many of these legitimate nested loadout sub-entries and would be the wrong number to fix against.

### 7.2 Revising the earlier "468 vs. ~2,300" statement

The previous E2 status update told you the ~468-vs-~2,300 gap against `docs/Reference-App-Analysis.md` was
"likely a different counting basis" in the reference app. That explanation is now superseded: **it is
primarily this mapping bug.** 466 (mapped) + 1,053 (dropped) = 1,519 structurally-present standalone
datasheets is already more than 3× the current result and gets most of the way to ~2,300. The residual gap
between 1,519 and ~2,300 has not been investigated and may still involve a genuinely different counting basis
(e.g., Warscribe possibly listing wargear/upgrade variants or per-detachment duplicates as separate rows) —
but that residual question is secondary to the primary, fixable bug identified here.

---

## 8. Step 5 — Search & filtering (after mapping, unrelated to the bug above)

**File:** `RulesData/Search/DatasheetSearchService.cs`

`Search()` applies filters as a chain of `.Where()` clauses — **all conditions are AND'd together**:

1. `MatchesLegends` — `datasheet.Name.Contains("[Legends]")` must equal the current `ShowLegends` toggle
   state. This is a **substring check on the display name**, not a dedicated data field — any datasheet whose
   name happens to contain the literal text `[Legends]` is treated as a Legends entry.
2. `MatchesQuery` — depending on `Mode` (`Name` / `RulesText` / `All`), checks the name and/or a broad sweep
   of keywords, ability names/descriptions, and weapon names/keywords for the query substring.
3. `MatchesFacet([Faction], FactionFilters)` and `MatchesFacet(Keywords, RoleFilters)` — **within** a facet,
   `Include` values are OR'd together and `Exclude` always wins over `Include` (exclude-list checked first,
   short-circuits to `false` if any excluded value matches).
4. `MatchesCharacteristic` — parses the *leading digits* of the raw stat-line string (`TryParseLeadingInt`,
   e.g. `"3+"` → `3`) and compares against the selected minimum. A stat line whose value has no leading digit
   (e.g. a bare `"-"`, as seen in the four known residual blank-Movement cases) yields `null` and is filtered
   out entirely by this facet if active.

The **Role** chip list (`RoleFilterOptions.All`, 18 fixed strings) is a **hardcoded list**, not derived from
the actual data. It was captured from one earlier manual session of the reference app's UI. Any keyword
present in the mapped data but not in this fixed list (there could be many, since `Keywords` is populated
directly from each unit's `CategoryLinks` with no allow-list) simply has no chip to filter by — it doesn't
hide those datasheets from the unfiltered view, but it means the Role facet is not guaranteed to be complete
relative to the real keyword space. This is a separate, lower-severity gap from the `"model"` bug and does not
affect total result counts when no Role filter is active.

---

## 9. Step 6 — Rendering

**File:** `RulesData/Pages/Scry.razor`

- `_allDatasheets` is loaded exactly once in `OnInitializedAsync` and never refreshed for the lifetime of the
  page (no re-fetch button, no polling).
- `_filtered` (the search result) is bound to a Blazor `<Virtualize>` component inside a `<table>`, so only
  visible rows are actually rendered to the DOM regardless of result-set size.
- Clicking a row sets `_selected`, which is passed straight through to `<DatasheetDetail Datasheet="_selected" />`
  (`RulesData/Components/DatasheetDetail.razor`), which renders the stat-line table, ranged/melee weapon
  tables (only shown if non-empty), `<AbilityList>` for Named/Wargear/Core abilities (only shown if
  non-empty), keywords, and the composition-constraint badge.

Nothing in the rendering layer filters or drops datasheets — by the time data reaches `Scry.razor`, the
`"model"`-typed datasheets are already gone (dropped in step 5/§5), so the UI has no way to know they were
ever supposed to be there.

---

## 10. Summary — where exactly the data is lost

| Stage | File | What happens to `"model"`-typed datasheets |
|---|---|---|
| 1. Manifest | `BsDataCatalogueManifest.cs` | Unaffected — file list is correct |
| 2. Fetch | `DatasheetCatalog.cs` | Unaffected — whole file is fetched, including `"model"` entries |
| 3. Deserialize | `BsData/*.cs` | Unaffected — `BsEntry.Type` correctly captures `"model"` as a string |
| 4. Index | `BsDataCatalogueMapper.IndexEntries` | Unaffected — indexed into `entriesById` like any other entry |
| **5. Select datasheets** | **`BsDataCatalogueMapper.Map`, line 54** | **`.Where(e => e.Type == "unit")` silently discards every `"model"` entry here — this is the bug** |
| 6. Cache | `IndexedDbRulesDataStore.cs` | Unaffected — caches whatever `Map()` returned (already incomplete) |
| 7. Search/filter | `DatasheetSearchService.cs` | Unaffected — operates correctly on the (incomplete) input it receives |
| 8. Render | `Scry.razor` / `DatasheetDetail.razor` | Unaffected — renders correctly whatever reaches it |

The original version of this section ended here, stating no code had been changed. That has since changed —
see §11.

---

## 11. Fix applied (post-review)

Two changes in `BsDataCatalogueMapper.cs`:

1. The `Map()` selection loop now iterates `catalogue.SharedSelectionEntries` directly (not `entriesById.Values`)
   and accepts both `Type == "unit"` and `Type == "model"`.
2. **A second issue was found while implementing the fix, not covered above:** naively widening the *existing*
   `entriesById.Values.Where(...)` filter to include `"model"` (instead of switching to
   `catalogue.SharedSelectionEntries` directly) would have also promoted **nested** `"model"`-typed entries —
   e.g. individual per-model loadout variants inside a squad, like `"Boy w/ Big shoota"` nested inside the
   `"Boyz"` unit — into their own standalone datasheets. `entriesById` is a flat index of *every* entry reached
   anywhere in the recursive walk, not just top-level ones, so filtering it by `Type` alone cannot distinguish
   "a real standalone datasheet" from "a loadout variant that happens to share the same `Type` string, nested
   three levels deep inside a real datasheet's own subtree." Filtering `catalogue.SharedSelectionEntries`
   itself (the top-level array only) avoids this — `MapUnit`/`CollectFromSubtree` still uses the full
   `entriesById` index internally to resolve cross-references, which is unaffected and correct.

Both issues are now covered by regression tests in `BsDataCatalogueMapperTests.cs`
(`Maps_a_single_model_datasheet_that_is_typed_as_model_instead_of_unit`,
`Does_not_turn_a_squad_units_nested_per_model_loadout_variants_into_separate_datasheets`).

**Live-verified counts** (IndexedDB cache cleared, full 45-file catalogue reload):

| Scope | Before fix | After fix |
|---|---:|---:|
| Orks, non-Legends | 18 | 63 |
| Orks, Legends | (not separately checked) | 15 |
| Orks, total | 21 | 78 |
| All factions, non-Legends | 381 | 1,148 |
| All factions, Legends | 87 | 371 |
| All factions, total | 468 | **1,519** |

1,519 matches exactly the "top-level unit + top-level model" figure calculated in §7 (466 + 1,053), confirming
the fix captures precisely the intended set — no more, no less. The residual gap against Warscribe's ~2,300 and
Orks' specific 67 (1,519 vs ~2,300 overall; 63/78 vs 67 for Orks) was not investigated further and may still
involve a different counting basis on the reference app's side (see §7.2's original caveat, now the remaining
open question rather than the primary explanation).

---

## 12. Current flow (merged single source of truth)

```
Build time:  MSBuild target MergeRulesData ──▶ Servitor.DataMerge ──▶ wwwroot/data/merged/{rules-data.json, manifest.json, merge-report.md}
In the app:  "Merge data" button (top nav) ──▶ RulesDataMergeService ──▶ same RulesDataMerger, result stored in IndexedDB

Browser opens /scry
        │
        ▼
Scry.razor : OnInitializedAsync()  ──▶ IDatasheetCatalog.GetAllAsync()        (RulesData/Loading/DatasheetCatalog.cs)
        │
        ▼
IMergedRulesDataProvider: manifest of the IndexedDB copy vs. manifest of the shipped merged/ files
        │  the newer GeneratedAtUtc wins; requests bypass the browser cache; a dev-server SPA fallback counts as "file missing"
        ▼
merged/rules-data.json (or the IndexedDB copy) ──▶ Datasheets in memory ──▶ DatasheetSearchService ──▶ virtualized table (§8–§9)
```

- The merge reads the 45 BSData catalogues, `core/` and `factions/` through `IRulesSourceReader` (file system in the tool, HTTP in the browser). BSData datasheets pass through unchanged, faction stratagems and stratagem ids per datasheet come from the rules-text source, and missing core rules are added. Rules and layout: [CODING_GUIDELINES.md](../CODING_GUIDELINES.md) "Data & Assets".
- `DatasheetCatalog` raises `Changed` after the "Merge data" button stored a new result, and Scry reloads. Without any merged data Scry shows the hint "No rules data yet. Use “Merge data” in the top bar to build it."
- Verified on the real data: 1519 datasheets (1148 current + 371 Legends), source, merged and displayed counts identical for all 36 Scry faction labels.
