# Repository Guidelines

## Project Structure & Module Organization

Game code lives in `Assets/Game/Scripts/`: `Domain` contains gameplay rules and immutable data; `Application` contains coordination and contracts; `Runtime` contains Unity adapters, UI, content, and authoring; `Editor` contains scene/content generation and verification tools. Keep Domain and Application free of Unity APIs.

Tests live in `Assets/Game/Tests/{Domain,EditMode,PlayMode}`. Scenes, settings, audio, materials, and prefabs occupy corresponding folders under `Assets/Game/`. Consult `Docs/team-ownership.md` for ownership boundaries and `Docs/chart-format-v1.md` for storage contracts. `Builds/` and `Logs/` hold build outputs and verification evidence.

## Build, Test, and Development Commands

Use Unity **6000.3.23f1**. Open `Assets/Game/Scenes/Bootstrap.unity` and enter Play Mode to run locally.

- **Game Tools > Scenes > Build All Scenes** regenerates authoritative scenes. Make lasting layout changes in builders.
- **Game Tools > Content > Prepare Default and Verification Content** prepares resources and recreates verification charts; use deliberately.
- **Game Tools > Verification > Verify Foundation (Play Mode and Player Build)** runs integration checks and builds `Builds/Windows/RhythmDojo.exe`.

For batch tests, run from the repository root in PowerShell:

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
& $unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Logs/editmode.xml" -logFile "$PWD/Logs/editmode.log"
```

Repeat with `PlayMode` and distinct result/log filenames. Close the project in Unity first, wait for each process to exit, and omit `-quit` for tests and asynchronous verification.

## Coding Style & Naming Conventions

Follow existing C# style: four-space indentation, brace-delimited namespaces, PascalCase types/methods/properties, camelCase parameters/private fields, and `I`-prefixed interfaces. Use `RhythmDojo` namespaces and descriptive filenames. No repository formatter configuration was found. Preserve Unity `.meta` files with their assets; avoid editing generated `.csproj` files or `Library/` caches.

## Testing Guidelines

Use Unity Test Framework 1.6.0 with NUnit. Name fixtures `*Tests` and methods after expected behavior. Cover pure rules in Domain tests, asset/editor behavior in EditMode, and lifecycle/input/UI integration in PlayMode. No coverage percentage is specified. Run affected suites; run foundation verification for scene or gameplay integration changes.

## Commit & Pull Request Guidelines

Use concise imperative commit subjects. PRs should explain the behavior change, link relevant issues, report test/build results, and include screenshots for visual changes. Coordinate shared-contract changes with affected owners.
