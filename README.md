# Servitor

An offline-capable army list tool that runs entirely in your browser. Import a list, build or edit it, look up unit datasheets, keep score, and track a game as it is played. After the first load, no server or internet connection is needed.

Servitor is a Blazor WebAssembly app (.NET 10). It can be installed as a PWA.

## Quick start with Servo

Servo is a small launcher with a skull-and-gear icon. Start it and it serves the app on your own computer and opens it in a full-screen window. Closing that window stops Servo. Nothing is installed and no internet connection is needed once the rules data is loaded.

| System | Package | Start |
| --- | --- | --- |
| Windows | `Servo-win-x64.zip` | Unzip, double-click `Servo.exe` |
| macOS (Apple Silicon) | `Servo-osx-arm64.zip` | Unzip, double-click `Servo.app` |
| macOS (Intel) | `Servo-osx-x64.zip` | Unzip, double-click `Servo.app` |
| Linux (x64) | `Servo-linux-x64.tar.gz` | Extract, run `Servo` |

Requirements: Microsoft Edge, Google Chrome, Chromium or Brave. Servo uses one of them for the full-screen window; it does not touch your normal browser profile. Keep the `wwwroot` folder next to the program. For the first rules data update, Git must be installed.

**First start:** the app has no rules data yet. Open the settings menu (gear icon, top right), choose **Update data** and wait until it reports "Update finished". This needs an internet connection once. Your armies, scores and the rules data are stored per user (Windows: `%LOCALAPPDATA%\Servo`, macOS and Linux: `~/.local/share/Servo`) and stay when you replace Servo with a newer version.

Notes for systems that distrust unsigned programs:

- **Windows:** SmartScreen may warn on first start. Choose *More info*, then *Run anyway*.
- **macOS:** Servo is not signed. Right-click `Servo.app`, choose *Open*, and confirm. If macOS reports the app as damaged, run `xattr -cr Servo.app` and then `codesign --force --deep -s - Servo.app` in the Terminal.
- **Linux:** if your file manager opens the file as text, enable running executable files in its settings, or start it from a terminal.

### Building the packages

```bash
python scripts/publish_servo.py
```

This writes the four packages to `artifacts/` (use `--only win-x64` to build a single one). It needs the .NET SDK and Python 3, plus internet access for the runtime packs of the other systems. The packages contain the app without rules data.

The icon is drawn by `scripts/make_icon.py` (needs Pillow); the generated files are committed.

## Running from source

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A current desktop browser (Chromium-based, Firefox or Safari)
- Git, only needed to fetch or update the rules data (see [Loading the rules data](#loading-the-rules-data))

### Running the app

From the repository root:

```bash
dotnet watch run --project src/Servitor.App/Servitor.App.csproj --launch-profile http
```

Then open <http://localhost:5025>.

To build and run the tests:

```bash
dotnet build
dotnet test
```

### Loading the rules data

The rules data (datasheets, stratagems, core rules) is not part of the repository. It lives in `src/Servitor.App/wwwroot/data/` and is generated from external sources. A fresh clone therefore has to fetch it once.

1. Start the local update service in a second terminal:

   ```bash
   dotnet run --project src/Servitor.DataUpdate
   ```

   It listens on `http://localhost:5180` and only accepts requests from localhost.

2. In the running app, open the settings menu (gear icon, top right) and choose **Update data**.

The update downloads the sources, converts them and runs the merge. The app then uses the merged result. The service is only needed for updating; once the data is loaded, the app works without it.

The merge can also be run on its own:

```bash
dotnet run --project src/Servitor.DataMerge
```

or from inside the app with the **Merge data** button in the top navigation, which shows a report of what was merged and lets you download it.

## Using the app

The top navigation has five sections.

### Import

The landing page. Paste an army list export into the text box and press **Import & Play**. If the text cannot be read, the problems are listed below the button. On success you are taken straight to Play.

The expected input is a plain-text export with a header block (faction, detachment, total points, warlord), one line per unit with its wargear, enhancement lines, and `Leading:` lines that attach a character to a unit. Example lists are in the `data/` folder.

Other links on this page:

- **Build one instead** starts an empty army in Build.
- **My Collection** lists the armies you have saved. Reopen one for editing or go directly to Play.

### Build

Create or edit an army by hand:

1. Choose the battle size, faction and detachment(s).
2. Add units from the catalog, one tab per faction. Factions that your faction may ally with get their own tab, and allied units are flagged in the list.
3. Configure each unit's wargear.
4. Watch the running point total and the validity indicator. Rule violations are listed so you can see what to fix.

### Scry

A searchable datasheet browser. Search by name, filter by faction and by characteristics, and open a datasheet for stats, weapons, abilities and keywords.

### Scoring

Set up the mission, then track victory points round by round for both sides. At the end of the game, an end-game scorecard is saved to the history.

### Play

The battle tracker:

- Round, turn and phase controls, plus command points for each side.
- An action queue showing the stratagems and abilities relevant to the current phase, including usage-limit tracking.
- Every unit of your army as a datasheet card, with per-model wound tracking.
- Opponent's army: load an opponent's list to see their units and rules.
- A commands view with the full list of stratagems.
- A scoring shortcut.

Game state survives a page reload.

## Offline use and your data

All of your data stays on your device. Armies, scores and the current game are stored in the browser's IndexedDB. Clearing the site data for this app removes them.

To use the app offline, load it once while online (or from your local server) and install it as a PWA from your browser's address bar or menu.

## Project layout

| Path | Contents |
| --- | --- |
| `src/Servitor.App` | The Blazor WebAssembly app |
| `src/Servitor.RulesData` | Domain model, source mappers and the merge logic |
| `src/Servitor.DataMerge` | Console tool that runs the merge |
| `src/Servitor.DataUpdate` | Local service that fetches and converts the rules data |
| `src/Servitor.Launcher` | Servo, the double-clickable launcher |
| `scripts/` | Packaging and icon scripts |
| `tests/Servitor.App.Tests` | Unit and component tests |
| `docs/` | Backlog and analysis documents |

Coding conventions are in [CODING_GUIDELINES.md](CODING_GUIDELINES.md).
