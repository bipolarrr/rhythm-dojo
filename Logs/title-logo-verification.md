# Title logo and expandable menu verification

Verified on Windows, 2026-10-03, using Unity 6000.3.23f1.

- Circular title logo and text bounce together on the configured BPM; both title and prompt retain their red glow.
- Clicking the logo expands a horizontal Play/Settings/Quit menu; clicking again collapses it.
- Background and hidden menu buttons do not start gameplay. Quit confirmation blocks menu input.
- Settings opened from the title returns to the title. Song-selection settings retains its existing return behavior.
- `Assets/Game/Settings/TitleMusic.asset` accepts an optional looping clip, BPM, first beat time and volume. The scene builder preserves this asset reference.
- Foundation verification passed: 106 EditMode tests, 32 PlayMode tests, and the Windows standalone build with Title as the entry point.
- `SceneBuilder.BuildTitle` also completed successfully in the current project after opening the existing Title scene for the batch invocation. The temporary invocation helper was removed afterward.

GitHub integration uses `https://github.com/bipolarrr/rhythm-dojo.git`, with `origin/main` at `79b85bcedfc9d65216b717b9d31eadfc5c233288`. The affected existing scripts were compared with this main revision; their changes correspond to this title feature. The local feature branch is `codex/title-logo-menu`. Unity-generated package and personal environment changes are excluded from the staged feature.

Project-local UnityYAMLMerge and the pre-commit metadata hook were configured. The staged index passed metadata validation with 191 meta files and `git diff --cached --check`. `Tools/Test-UnityGitSetup.ps1` passed the valid/invalid metadata cases, retained independent prefab edits during a merge, and left an unresolved conflict available for manual resolution. No remote push was performed.

Raw XML, logs and screenshots are retained locally in `Logs/title-logo-*`; build output is `Builds/Windows/RhythmDojo.exe`. They remain ignored according to the repository rules.
