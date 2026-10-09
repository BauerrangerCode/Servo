# Code Review — 2026-09-18

Scope: uncommitted working-tree changes on `main` at the time of review — the Scry data pipeline / search
feature (E1 bootstrap + in-progress E2 work). Reviewed against `CODING_GUIDELINES.md` and `CLAUDE.md`.

Verified before review:

- `dotnet build`: 0 warnings, 0 errors
- `dotnet test`: 44/44 passing
- `dotnet format --verify-no-changes --no-restore`: no formatting deviations

This document reports findings only. No code was changed as part of this review.

> **Status update (after the data source merge):** this review is a point-in-time record. Current state of the findings:
>
> | # | Finding | Current state |
> |---|---|---|
> | 1 | Rules data committed under `wwwroot/data/` | Resolved by decision. `wwwroot/data/` is gitignored (`.gitignore`, comment there) and documented in CODING_GUIDELINES.md "Data & Assets": `wwwroot` is the runtime home, `/data/` the pull source. |
> | 2 | `"model"` entries dropped by the mapper | Fixed (see [Scry-Data-Pipeline.md](Scry-Data-Pipeline.md) §11). Real-data test: all 1519 datasheets are mapped. |
> | 3 | Unhandled fetch failures, discarded warnings | Superseded. Loading goes through `MergedRulesDataProvider` (missing files are a handled case), and mapper/merge warnings end up in the merge report and the "Merge data" dialog. |
> | 4 | Hardcoded Role facet list | Unchanged, open, low impact. |
>
> File paths in the findings below are the pre-merge paths (`src/Servitor.App/RulesData/Loading/…`, `Mapping/…`); the mappers and domain records now live in `src/Servitor.RulesData/`.

---

## 1. Critical — data directory contradicts CODING_GUIDELINES.md ("Data & Assets")

**Files:** `src/Servitor.App/RulesData/Loading/BsDataCatalogueManifest.cs`,
`src/Servitor.App/wwwroot/data/bsdata/wh40k-11e/*.json`, `.gitignore`

`CODING_GUIDELINES.md` § *Data & Assets* states:

> All Warhammer-rules-related data and other assets... live under `/data/` at the repo root. `/data/` is
> gitignored — it is never committed.

In the current working tree, 46 BSData catalogue JSON files (~49 MB total) live under
`src/Servitor.App/wwwroot/data/bsdata/wh40k-11e/`, copied there from the gitignored `/data/` source
(comment in `BsDataCatalogueManifest.cs:5`: *"copied from the gitignored /data/ source... so the browser can
fetch them"*). `.gitignore` only excludes `/data/` at the repo root (line 33) — the `wwwroot/data/` copy is
**not** covered and shows as untracked (`??`) in `git status`. A plain `git add` would commit ~49 MB of GW
rules data into the repository permanently.

**Root cause:** Blazor WebAssembly can only fetch static assets via `HttpClient` from files served out of
`wwwroot`, so the app-level `/data/` source (correctly gitignored per the guideline) cannot be read directly
by the browser at runtime. The current implementation works around this by duplicating the data into a
git-tracked location, which reintroduces exactly the situation the guideline was written to prevent.

This is an architecture-level decision with long-term repository impact (data location, repo size, git
history), not an implementation detail — it falls under CLAUDE.md's "confirmation required" categories
("Architecture decisions with long-term impact", "Deviations from an agreed task's scope"). Flagged here for
a decision; no fix is proposed or applied as part of this review.

---

## 2. Critical — confirmed, unfixed mapping bug drops 69% of datasheets

**File:** `src/Servitor.App/RulesData/Mapping/BsDataCatalogueMapper.cs:54`

```csharp
foreach (var entry in entriesById.Values.Where(e => e.Type == "unit"))
```

This filter silently discards every `"model"`-typed entry. Per the existing investigation in
`docs/Scry-Data-Pipeline.md` (already committed prior to this review, root cause confirmed there), this drops
**1,053 of 1,519** structurally-present standalone datasheets (69%) across all 45 manifest files — every
faction is affected, not just the originally reported Orks case.

The investigation doc states "no code has been changed as a result of this investigation" — that is still
true at review time; the bug is present in the current working tree unchanged.

**Test coverage gap:** `BsDataCatalogueMapperTests.cs` has no test that exercises a `"model"`-typed top-level
entry. All current mapper tests (including the new stat-line regression test) only cover `"unit"`-typed
entries. Per CODING_GUIDELINES.md § *Tests* ("every test must be able to catch a real regression"), this
confirmed, quantified, high-severity bug currently has no regression test guarding a fix — or preventing
recurrence — once addressed.

---

## 3. Medium — unhandled fetch failures and discarded mapper warnings

**File:** `src/Servitor.App/RulesData/Loading/DatasheetCatalog.cs:40-46`

```csharp
var url = $"data/bsdata/wh40k-11e/{Uri.EscapeDataString(fileName)}";
var json = await httpClient.GetStringAsync(url, cancellationToken);
var result = BsDataCatalogueMapper.Map(json);

await store.SaveFactionAsync(
    new FactionRulesData(fileName, BsDataCatalogueManifest.DataVersion, result.Datasheets, [], [], []),
    cancellationToken);
```

Two issues:

- `httpClient.GetStringAsync` is called with no `try`/`catch`. This is an external boundary (network fetch of
  a static file) per CODING_GUIDELINES.md § *Error Handling* ("Validate at boundaries"). If a single catalogue
  file is missing or a fetch fails (e.g. after a manual manifest edit that falls out of sync with the files on
  disk — `BsDataCatalogueManifest.cs` is hand-maintained, see its own doc comment), `GetAllAsync` throws
  unhandled and `Scry.razor` (`OnInitializedAsync`) has no error boundary — the page is stuck on "Loading rules
  data…" indefinitely with no user-visible error.
- `result.Warnings` from `BsDataCatalogueMapper.Map` is discarded here (`FactionRulesData` is constructed with
  `[]` for the warnings-shaped fields). If the mapper skips a unit with a warning, there is currently no path
  for that information to reach the UI or the user. This is not an empty `catch` block, but it is the same
  category of problem the Error Handling guideline is meant to prevent: a failure that is silently absorbed
  instead of surfaced. Already self-flagged in `docs/Scry-Data-Pipeline.md` § 3; repeated here as a guideline
  finding rather than an implementation-notes aside.

---

## 4. Minor — hardcoded Role facet list of unverified completeness

**File:** `src/Servitor.App/RulesData/Search/RoleFilterOptions.cs`

The 18-entry `Role` chip list is a hardcoded array captured manually from one session against the reference
app, not derived from the actual mapped `Keywords` data. Any keyword present in real data but missing from
this list has no filter chip. Low severity — it doesn't hide datasheets from the unfiltered view, and it is
already self-documented in `docs/Scry-Data-Pipeline.md` § 8. Noted here for completeness since it's a
data-vs-hardcoded-value pattern the guideline generally asks to avoid outside of true constants.

---

## 5. Guideline-conformant — no action needed

- **Feature-folder structure**: `RulesData/{Domain,Mapping,Loading,Search,Storage,Components,Pages}` matches
  the feature-based structure required by CODING_GUIDELINES.md § *Project Structure*.
- **External/domain separation**: `Mapping/BsData/*` (raw BattleScribe shape) is kept fully separate from
  `Domain/*` (immutable records); mapping goes through `BsDataCatalogueMapper`, never direct deserialization
  into domain types.
- **Persistence**: `IndexedDbRulesDataStore` is a thin, hand-written JS-interop wrapper with no ORM
  dependency, one JSON document per faction/version key — matches § *Local Data Persistence* exactly.
- **Error handling in the mapper**: `BsDataCatalogueMapper.Map`'s per-unit `catch` is narrowly typed
  (`KeyNotFoundException or InvalidOperationException or NullReferenceException`) and turns failures into
  warnings rather than crashing or swallowing silently — correct per § *Error Handling*.
- **Comments**: all comments found are WHY-only (e.g. the two-shape stat-line resolution comment in
  `BsDataCatalogueMapper.cs`, the `[Legends]`-marker caveat in the search service) — none describe WHAT the
  code does.
- **Component thinness**: `Scry.razor`'s code-behind is UI-state bookkeeping and event forwarding only; the
  actual filter/search business logic lives in `DatasheetSearchService`, a static class fully testable without
  rendering. `DatasheetDetail.razor` / `AbilityList.razor` are pure rendering.
- **Tests**: all new tests are behavior-based (bUnit for `Scry.razor`, xUnit for search/mapping logic), include
  targeted regression tests (e.g. the Aeldari inline-stat-line fix), and none exist purely to hit a coverage
  number.
- **Language**: all code, identifiers, comments, and docs in this change set are English-only, per
  CODING_GUIDELINES.md / CLAUDE.md.

---

## Summary

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | Critical | Rules data (49 MB) committed under `wwwroot/data/`, not gitignored, contradicts "Data & Assets" | Needs a decision, not a code fix |
| 2 | Critical | `Map()` drops all `"model"`-typed entries — 69% of datasheets missing | Confirmed, unfixed, untested |
| 3 | Medium | No error handling around catalogue fetch; mapper warnings discarded | Open |
| 4 | Minor | Role facet list hardcoded, not verified against real keyword data | Open, low impact |
