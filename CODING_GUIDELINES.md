# Servitor V0.1 — Coding Guidelines

Binding, not aspirational. Deviations are treated as review findings, not silently accepted.

## Language

All code, identifiers, comments, commit messages, and technical documentation are in English — no exceptions. Chat with the user stays German (see [CLAUDE.md](CLAUDE.md)).

## Stack

- .NET 8 (LTS), Blazor WebAssembly (standalone, PWA-capable)
- Nullable reference types: enabled (`<Nullable>enable</Nullable>`), warnings as errors in release builds
- Tests: xUnit for business logic, bUnit for component tests

## Project Structure

Feature-based, not layer-based. No project-wide top-level `Controllers/`, `Models/`, `Services/` folders — instead, per feature (e.g. `ArmyImport/`, `BattleTracker/`, `RulesEngine/`) with its own components, services, and models. Genuinely generic shared building blocks go in `Shared/` or `Common/`.

Rationale: the reference app has clearly separated feature areas (import, build, battle tracker/scry, scoring). Feature folders keep related code together instead of spreading one feature's change across three layer folders.

## Data & Assets

- All Warhammer-rules-related data and other assets (images, 3D models, etc.) live under `/data/` at the repo root. `/data/` is gitignored — it is never committed.
- Source for rules data (this iteration): [BSData/wh40k-11e](https://github.com/BSData/wh40k-11e). Planned to extend to Age of Sigmar and Spearhead BSData repositories later.
- Suggested layout: `/data/bsdata/<game-system>/` (e.g. `/data/bsdata/wh40k-11e/`) for cloned/pulled BSData catalogues, `/data/assets/` for images/3D models, kept separate since they have different lifecycles and storage needs.
- A future feature will let the user refresh this data via a button or command (re-pull the BSData source, re-parse into the internal format). Not part of V0.1 — do not build this ahead of being asked (see [CLAUDE.md](CLAUDE.md) Scope Discipline).

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

- Parsed rules data (from BSData catalogues) is stored as compact JSON documents in IndexedDB, one entry per faction/catalogue, keyed by faction id + data version.
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
