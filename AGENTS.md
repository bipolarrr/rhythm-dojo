# Repository Guidelines

## Development Baseline

`main` is the authoritative development baseline and integration target. The user has retired the independent `song` baseline; do not resume work from it or restore its version over `main`. Perform the requested repairs on `main` unless the user specifies another branch. The user authorizes and decides this repair work; historical team ownership documents provide architectural context and do not require teammate approval for user-requested fixes.

## Project Structure & Module Organization

Game code lives in `Assets/Game/Scripts/`: `Domain` contains gameplay rules and immutable data; `Application` contains coordination and contracts; `Runtime` contains Unity adapters, UI, content, and authoring; `Editor` contains scene/content generation and verification tools. Keep Domain and Application free of Unity APIs.

Tests live in `Assets/Game/Tests/{Domain,EditMode,PlayMode}`. Scenes, settings, audio, materials, and prefabs occupy corresponding folders under `Assets/Game/`. Consult `Docs/team-ownership.md` for ownership boundaries and `Docs/chart-format-v1.md` for storage contracts. `Builds/` and `Logs/` hold build outputs and verification evidence.

## Build, Test, and Development Commands

Use Unity **6000.3.23f1**. Open `Assets/Game/Scenes/Title.unity` and enter Play Mode to run the complete menu flow locally. `ModeSelection` opens the four mode rooms directly; `Bootstrap` and `SongSelection` are direct free-practice entry points. Disable **Start Play Mode From Title** when checking a scene directly.

- **Game Tools > Scenes > Build All Scenes** regenerates authoritative scenes. Make lasting layout changes in builders.
- Do not add standalone Open scene commands to Game Tools or other custom editor menus. Open existing scenes through the Project window; scene builders may open the scenes they generate.
- **Game Tools > Content > Prepare Default and Verification Content** prepares resources and recreates verification charts; use deliberately.
- **Game Tools > Verification > Verify Foundation (Play Mode and Player Build)** runs integration checks and builds `Builds/Windows/RhythmDojo.exe`.

With the project open in Unity, prefer the installed Unity CLI and Pipeline package (`com.unity.pipeline`):

```powershell
unity status --json
unity command run_tests --mode playmode --filter SelectionBrowserTests --async_tests true --json
unity command test_status --json
```

Poll `test_status` until completion and inspect the test summary and failures. Use `--mode editor` for EditMode tests; omit `--filter` to run the entire mode. Run one suite at a time. Results may arrive before Test Runner cleanup finishes: wait for Play Mode to exit and confirm `unity status --json` is ready before starting another run. After source changes, refresh/recompile the Editor before testing. If the package was just installed and the connection is unavailable, use **Assets > Refresh** (Ctrl+R) and wait for compilation. These commands use the running Editor; `unity test` and the batch commands below launch a separate process and require the project to be closed.

For batch tests, run from the repository root in PowerShell:

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
& $unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Logs/editmode.xml" -logFile "$PWD/Logs/editmode.log"
```

Repeat with `PlayMode` and distinct result/log filenames. Close the project in Unity first, wait for each process to exit, and omit `-quit` for tests and asynchronous verification.

## Coding Style & Naming Conventions

Before adding or changing code, read the corresponding implementation on the local Git `main` branch and follow its patterns for structure, reuse, and style.

Follow existing C# style: four-space indentation, brace-delimited namespaces, PascalCase types/methods/properties, camelCase parameters/private fields, and `I`-prefixed interfaces. Use `RhythmDojo` namespaces and descriptive filenames. No repository formatter configuration was found. Preserve Unity `.meta` files with their assets; avoid editing generated `.csproj` files or `Library/` caches.

### Runtime creation and authored composition

- Create objects, components, and resources at runtime only when runtime data or lifetime requires it. Fixed screen layouts, controls, cameras, lights, audio sources, decorative children, and fixed-size effect pools belong in scenes or prefabs with serialized references.
- Keep lasting composition in the corresponding editor builders and regenerate affected assets. Runtime controllers bind services, update state, and animate authored views; they must not reconstruct fixed UI or silently repair missing required composition with `AddComponent`, name-based searches, or ad hoc object creation. Validate missing references explicitly.
- Runtime creation remains appropriate for data-dependent counts (song rows, chart notes, mode lanes), imported user content, size/device-dependent buffers, isolated mutable session data, and instances with a genuinely dynamic lifetime. Instantiate authored templates for their fixed structure, reuse instances when practical, and define ownership and cleanup.
- Bake deterministic image transformations and other fixed presentation resources in the editor. Share immutable materials and assets; clone only when an instance needs independent mutation. Record the runtime requirement near non-obvious creation sites, and test that fixed composition survives initialization and reopening without replacement.

### Branches and conditions

- Avoid ternary expressions (`?:`); use explicit `if`/`else` blocks or a readable `switch`. Never introduce nested or chained ternaries. Null-conditional (`?.`), null-coalescing (`??`), and nullable type syntax are separate features and may be used normally.
- Keep value selection readable in method arguments, object initializers, and interpolated strings too. Use explicit branches when an embedded selection makes the surrounding expression difficult to follow. Do not disguise a complex ternary as a nested switch expression.
- Split a condition when it contains three or more logical operators (`&&`/`||`), exceeds roughly 120 characters, or combines unrelated checks. Wrapping the same large expression across several lines is not sufficient.
- Prefer early returns, `continue`, or explicit validation guards for invalid states and missing dependencies. Follow those checks with the normal processing path. Use short nested checks for dependent conditions; aim for no more than two levels of conditional nesting within a logical step.
- Group only closely related checks, usually two or three predicates per condition. Use a `switch` for distinct states or cases when it makes the alternatives clearer. Apply the Naming preservation rules below to all restructuring.
- Preserve evaluation order, short-circuit behavior, null safety, `out var` scope, exceptions, and event/timing order when splitting expressions. Keep Unity object truth checks intact.

### Statements and spacing

- Write one statement per line. Expand inline constructors, methods, and control-flow blocks that contain multiple operations. Use braces for C# `if`, `else`, `for`, `foreach`, `while`, and `do` bodies, including single-statement bodies.
- Separate logical steps with one blank line: input/dependency validation, local preparation, calculation, state changes, and notifications or return. Keep related assignments, subscriptions, and assertions together; do not insert a blank line after every statement.
- Separate methods and type declarations with a blank line. Group fields and simple properties by their role, adding spacing between groups where useful.
- Apply these readability principles to runtime code, editor tools, tests, and repository scripts. Formatting tools can assist, but manually review branching and logical grouping.

## Testing Guidelines

Use Unity Test Framework 1.6.0 with NUnit. Keep pure rules in Domain, editor/asset behavior in EditMode, and lifecycle/input/UI integration in PlayMode. Follow [Docs/verification.md](Docs/verification.md) for profiles, commands, cleanup, and evidence.

- Add tests only for new user behavior, a real regression, or a boundary existing tests miss. Extend or replace an existing case first. Do not grow tests that merely assign values, check names/structure, or repeat the implementation.
- Give every case exactly one cost category: `Fast`, `SceneIntegration`, `AssetGeneration`, `Hardware`, or `Endurance`. Keep feature categories independent; use `Compatibility` for supported legacy contracts.
- Start with the relevant cases after a coherent change. Add Smoke for scene composition or shared navigation. File count alone never justifies a full run. Do not repeat passing coverage without a relevant change or new failure evidence.
- Run all tests without a build when cross-layer impact is unclear or verification-runner/package contracts change. Add relevant Hardware for audio configuration/recovery and relevant Endurance for scheduled playback, song completion, or accumulated timing. Ordinary UI changes do not trigger those slow profiles. Build only for build/platform changes or an explicit request.
- Restore input devices, preferences, subscriptions, owned objects, files, GUIDs, and Editor state even on failure. Use observable completion with a deadline; retain waits that measure audio or animation duration. Do not share mutable scenes between cases.
- Lead reports with the change and user impact. Normally summarize verification in one paragraph with scope, result, and necessary limitations. Link detailed results/timings; do not list successful method names or long pass counts. Detailed lists belong to failures, unresolved items, or a requested audit. Never present partial verification as a full pass; zero cases, skips, and inconclusive results are not success.

## Commit & Pull Request Guidelines

Use concise imperative commit subjects. PRs should explain the behavior change, link relevant issues, report test/build results, and include screenshots for visual changes. Coordinate shared-contract changes with affected owners.

## Naming preservation

Do not rename existing variables, fields, properties, parameters, or files. Preserve existing functionality and interfaces; add new names only for new functionality.
