# Showcase core integration verification

Date: 2026-10-20
Unity: 6000.3.23f1
Branch: `integrate/showcase-core`, based on main `eb00f81`
Workspace: `C:/Users/song/RhythmDojo-showcase-core`

## Implementation

- Imported waveform peaks, preview playback, seeking/zooming, subdivision snapping, shared grid timing, and transactional grid edits.
- `ChartEditorSceneBuilder` calls `ChartAudioTimelineLayoutBuilder` to generate the timeline and advanced panel. Serialized `ChartAudioTimelineView` references connect controls; runtime code no longer searches for and relocates existing UI.
- The composition root injects the audio decoder. The scene owns the audio source and waveform label objects.
- The visible **고급 GRID** button opens tempo/meter editing, two-point BPM calculation, note preservation policy, and density controls. File/settings/tools panels are mutually exclusive with this panel. Basic and advanced grid edits respect busy/locked states.
- Existing Undo/Redo restores all charts and both hold endpoints as a single grid-edit operation.
- New saves use v3 with mandatory `timing.grid`. Strict v2 reads supply the default grid without rewriting files. v1 and showcase v2 have separate explicit import paths. The file menu exposes showcase import.
- Added v3 schema and compatibility/error-path/round-trip tests; retained the v2 schema and atomic save implementation.
- Regenerated `Assets/Game/Scenes/ChartEditor.unity`.

## Verification

| Check | Result |
| --- | --- |
| Foundation EditMode | 195 passed, 0 failed |
| Foundation PlayMode | 86 passed, 1 failed |
| Failed test rerun | `StrictHardDefaultsFailOnTheFirstGoodJudgment`: 1 passed, 0 failed |
| Windows standalone build | Succeeded, Title entry point |
| Source/test/document whitespace check | Passed |

The full PlayMode run failed an existing hard-mode test at its “Button must accept input” assertion. A separate rerun passed. The full Foundation run therefore was **not** an entirely green run; the Windows build was invoked separately after the rerun. The hard-mode test was not changed.

All authoring tests passed, including actual advanced-button clicks, panel exclusion, lock/busy guards, tempo/meter changes, BPM calculation, preserve-beats behavior, deletion, Undo/Redo, waveform generation, seeking/zooming, preview playback, and return from playtesting. The file-menu showcase import test verifies that ordinary loading rejects the extension, explicit import preserves the file/grid, and saving writes v3. Library integration verifies v3/grid preservation while selecting and playing all three chart slots.

Earlier batch verification exposed a zero-length overview label-array error, which was fixed, and a pre-existing `WaitForEndOfFrame` test incompatible with batch mode. Final verification used the normal Editor through Unity CLI. A scene-generation test now creates its own clean temporary scene, avoiding interference from dirty scenes left by preceding fixtures.

## Evidence

- [Default timeline, 1280×720](grid-timeline.png)
- [Advanced panel, 1280×720](grid-timeline-advanced.png)
- [Open dropdown, 960×540](grid-timeline-dropdown-small.png)
- Local full results: `verification-editmode.xml`, `verification-playmode.xml`, `verification.txt`
- Local retry result: `hard-mode-retry.json`
- Local Editor/build log: `editor-verification.log`
- Local build: `../Builds/Windows/RhythmDojo.exe` (the complete Windows output directory is required to run it)

Screenshots and this report are included with the change. XML, detailed logs, and build output remain local ignored verification artifacts.

## Isolation

The original `C:/Users/song/RhythmDojo` workspace was not edited. Its initial seven changes, and additional concurrent changes observed during verification, were left intact. Test-generated changes to unrelated scenes, materials, and project settings in this worktree were restored. This branch has not been merged into main because concurrent work also changed the original ChartEditor scene.
