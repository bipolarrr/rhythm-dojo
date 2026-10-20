# Repository Guidelines

## Project Structure & Module Organization

Game code lives in `Assets/Game/Scripts/`: `Domain` contains gameplay rules and immutable data; `Application` contains coordination and contracts; `Runtime` contains Unity adapters, UI, content, and authoring; `Editor` contains scene/content generation and verification tools. Keep Domain and Application free of Unity APIs.

Tests live in `Assets/Game/Tests/{Domain,EditMode,PlayMode}`. Scenes, settings, audio, materials, and prefabs occupy corresponding folders under `Assets/Game/`. Consult `Docs/team-ownership.md` for ownership boundaries and `Docs/chart-format-v1.md` for storage contracts. `Builds/` and `Logs/` hold build outputs and verification evidence.

## Build, Test, and Development Commands

Use Unity **6000.3.23f1**. Open `Assets/Game/Scenes/Bootstrap.unity` and enter Play Mode to run locally.

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

Follow existing C# style: four-space indentation, brace-delimited namespaces, PascalCase types/methods/properties, camelCase parameters/private fields, and `I`-prefixed interfaces. Use `RhythmDojo` namespaces and descriptive filenames. No repository formatter configuration was found. Preserve Unity `.meta` files with their assets; avoid editing generated `.csproj` files or `Library/` caches.

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

Use Unity Test Framework 1.6.0 with NUnit. Name fixtures `*Tests` and methods after expected behavior. Cover pure rules in Domain tests, asset/editor behavior in EditMode, and lifecycle/input/UI integration in PlayMode. No coverage percentage is specified.

Choose the smallest meaningful verification scope after a coherent set of changes, not after every file edit. Full PlayMode and Foundation runs are not the default:

| Change | Default verification |
| --- | --- |
| Documentation or comments only | Review the diff; no Unity tests |
| Domain/Application rules | Related Domain/EditMode fixtures |
| UI color, text, or layout | Related layout checks and visual inspection; add affected PlayMode tests when click targets or references change |
| One screen or gameplay feature | Affected fixture or test methods |
| Scene composition or shared navigation | Related tests plus **Verify Smoke Actions** |
| Contracts affecting several features, verification runner, packages, or unexplained regressions | **Verify All Tests (No Build)** |
| Build settings/platform changes or requested full delivery verification | **Verify Foundation (Play Mode and Player Build)** |

Use the CLI `--filter` or Test Runner selection for affected fixtures/methods. Smoke runs four existing navigation/input/cleanup tests without full-song scenarios or a player build; it does not replace feature-specific tests. See `Docs/verification.md` for the exact selection.

Do not repeat a passing run unless subsequent changes affect its coverage or new evidence warrants it. After failures, rerun the failing tests and affected scope first; expand only when the cause or impact remains unclear. Run Windows builds only for build-related changes or an explicit request. Report the executed scope, results, and reasons for omitted checks; never describe partial verification as a full pass. Keep the refresh, sequential execution, and Test Runner cleanup requirements above.

## Commit & Pull Request Guidelines

Use concise imperative commit subjects. PRs should explain the behavior change, link relevant issues, report test/build results, and include screenshots for visual changes. Coordinate shared-contract changes with affected owners.

## Naming preservation

Do not rename existing variables, fields, properties, parameters, or files. Preserve existing functionality and interfaces; add new names only for new functionality.
