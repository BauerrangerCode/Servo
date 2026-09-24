# Servitor V0.1 — Coding Guidelines

Binding, not aspirational. Deviations are treated as review findings, not silently accepted.

## Language

All code, identifiers, comments, commit messages, and technical documentation are in English — no exceptions. Chat with the user stays German (see [CLAUDE.md](CLAUDE.md)).

## Stack

- .NET 10 (LTS), Blazor WebAssembly (standalone, PWA-capable)
- Nullable reference types: enabled (`<Nullable>enable</Nullable>`), warnings as errors in release builds
- Tests: xUnit for business logic, bUnit for component tests

## Project Structure

Feature-based, not layer-based. No project-wide top-level `Controllers/`, `Models/`, `Services/` folders — instead, per feature (e.g. `ArmyImport/`, `BattleTracker/`, `RulesEngine/`) with its own components, services, and models. Genuinely generic shared building blocks go in `Shared/` or `Common/`.

Rationale: the reference app has clearly separated feature areas (import, build, battle tracker/scry, scoring). Feature folders keep related code together instead of spreading one feature's change across three layer folders.

Projects: `Servitor.App` (Blazor WebAssembly UI, feature folders as above), `Servitor.RulesData` (class library without any Blazor dependency: rules domain records, source mappers, the data merge), `Servitor.DataMerge` (console tool that runs the merge at build time). Code that both the app and the build tool need lives in `Servitor.RulesData`, never in `Servitor.App`.

## Data & Assets

- All Warhammer-rules-related data and other assets (images, 3D models, etc.) are external and gitignored — never committed.
- Two homes, because Blazor WebAssembly can only fetch static files via `HttpClient` from `wwwroot`:
  - `/data/` (repo root): the pull source. Currently only the clone of [BSData/wh40k-11e](https://github.com/BSData/wh40k-11e) at `/data/bsdata/wh40k-11e/` (a git repository — never copy its `.git` into `wwwroot`). Planned to extend to Age of Sigmar and Spearhead BSData repositories later; `/data/assets/` is reserved for images/3D models.
  - `src/Servitor.App/wwwroot/data/`: everything the app and the merge tool read at runtime.
    - `bsdata/wh40k-11e/*.json`: the catalogue JSON files copied from the clone. After pulling a newer BSData source, copy them here (no automated copy step yet) and update `RulesSourceManifest.BsDataVersion`.
    - `core/` and `factions/<faction>/`: the rules-text source (core rules, stratagems, faction/detachment/datasheet files). It is delivered directly into `wwwroot` and has no copy under `/data/`.
    - `merged/`: generated, see below. Never edit by hand.
- Raw source files are never modified. `Servitor.RulesData.Merging.RulesDataMerger` builds a separate merged single source of truth from them (`merged/rules-data.json`, `manifest.json`, `merge-report.md`), and the app reads only that.
- Merge rules: BSData is authoritative. Its datasheets and core rules pass through unchanged. From the rules-text source, the merge takes over (1) the stratagem ids per datasheet, joined by faction and normalized name (`FactionCatalogueMap` maps each BSData catalogue to its rules-text factions), (2) all stratagems (core and faction), converted from HTML to BSData-style Markdown and split into When/Target/Effect/Restrictions, and (3) the core rules BSData has no rule of that name for. Core rules carry a kind (`CoreRuleKind`: general, weapon rule, unit rule, action) so datasheets can link to them; cost-free entries of `core-stratagems.json` are actions, not stratagems (a stratagem always has CP). Stratagems carry a `GameMode` (standard, Boarding Actions, Challenger). Older duplicates in the core file are dropped by the merge and listed in the report. Everything that could not be matched is listed in the merge report, not guessed.
- The merge runs in two places with the same code: at build time (MSBuild target `MergeRulesData` in `Servitor.App.csproj`, incremental, skipped when the data is absent or with `-p:SkipRulesDataMerge=true`; manual run: `dotnet run --project src/Servitor.DataMerge`) and in the browser via the "Merge data" button in the top navigation, which stores the result in IndexedDB and shows/downloads the report. At startup the newer of the IndexedDB copy and the shipped `merged/` files wins.
- `RulesSourceManifest` lists every source file by hand (the browser cannot list a folder); tests keep it in sync with `wwwroot/data`.
- Not part of V0.1: a feature that re-pulls the BSData source itself (the merge only re-reads what is already in `wwwroot/data`) — do not build this ahead of being asked (see [CLAUDE.md](CLAUDE.md) Scope Discipline).

## Architecture / State Management

- No scattered state in individual Blazor components. State that spans more than one component (current army list, active battle session, turn/phase) lives in dedicated scoped DI services, not in component fields.
- Components stay as thin as possible: rendering + event forwarding. Business logic (rules evaluation, import parsing, wound calculation) belongs in services/classes outside `.razor` files so it is testable without rendering.
- No JS interop scattered through the code — wrap it in dedicated services.

## Data Model (Rules/Units/Stratagems)

- Game data (units, weapons, stratagems, rules) is modeled as pure, immutable C# records — no business logic inside the data records themselves.
- The external data source (BSData XML/JSON) and the internal domain model are kept separate: an explicit mapping/parsing step, never direct deserialization of BSData's catalogue format into internal domain types. External formats change outside our control; the domain must not depend on their shape.
- Rules data versioning is considered from the start (GW balance updates change values) — no hardcoding of rules values in logic code.

## Local Data Persistence (Rules Data & Assets)

No embedded SQL database for V0.1. Instead:

- The merged rules data (see Data & Assets) is stored as compact JSON documents in IndexedDB under fixed keys, next to a small manifest (data version + generation time) that decides whether the stored copy or the shipped files are newer.
- Binary assets (images, 3D models) are stored as Blobs in IndexedDB, keyed by asset id/path.
- Access goes through a thin, hand-written JS interop wrapper around the native IndexedDB API — no heavy ORM-style NuGet dependency.
- At runtime, the relevant faction's data is loaded into memory as C# records; search/filtering uses in-memory LINQ. No query engine is needed at this data volume.
- All storage access sits behind a service interface (e.g. `IRulesDataStore`), matching the State Management rule above — this keeps a later migration to a real embedded database (e.g. SQLite compiled to WASM, backed by OPFS) possible without touching consumers, if search/query needs later outgrow in-memory LINQ (e.g. true cross-faction/cross-game-system search) or asset sizes outgrow IndexedDB blob storage (in which case the File System Access API/OPFS as real file storage is the better fit, not IndexedDB).

## Error Handling

- No empty `catch` blocks, no generic `catch (Exception)` without a concrete reason and logging/rethrow.
- Validate at boundaries (import parsing of external text/JSON), not everywhere in internal code.
- A failed import (unparseable list) is an expected, ordinary case — no exception-based control flow for it; use result-style return types instead.

## Tests

- Business logic (parsing, rules evaluation, scoring, wound/damage calculation) is unit-tested without exception — this is the part most likely to break on GW rules updates.
- UI components are tested with bUnit where behavior (not just layout) is being verified.
- Claude runs these tests itself as part of every task; automate wherever possible. The dev server/test run process stays simple and documented so the user can also run and test manually at any time (see [CLAUDE.md](CLAUDE.md) Definition of Done) — automation does not remove that ability.
- No test is written to hit a coverage number — every test must be able to catch a real regression.

## Style

- No comments describing WHAT the code does — only when the WHY isn't obvious from the code itself (e.g. a workaround for a known BattleScribe export quirk).
- No god classes, no magic numbers (named constants, especially for 40K rules values like dice thresholds).
- No dead code paths, no commented-out code blocks in the merged state.
- Formatting via `.editorconfig` + `dotnet format`, not manually debated.
