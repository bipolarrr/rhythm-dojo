# Unity error follow-up - 2026-11-03

All identified failures have passing latest verification results. Existing variable, parameter, property and file names were not renamed. User recovery scenes and the pre-existing modified timeline images were preserved.

## Changes

- Removed five compiler warnings: legacy IME dummy-key rejection now avoids the obsolete enum reference, three intentionally asynchronous scene loads explicitly discard their return values, and the private write-only pausedAudioReset field was removed.
- PreparedSelectionScene distinguishes additive preparation from Single scene loads, releases ownership when an externally replaced scene unloads, drains pending cleanup before cancellation and keeps the last loaded view usable. The legacy HandleSceneLoaded(Scene) overload remains available.
- The selection EventSystem starts disabled in the builder and serialized scene. Direct composition enables it before screen initialization; prepared activation enables it after prior-scene cleanup. Existing scene object IDs were preserved.
- Synthetic test input is isolated from physical devices. A scoped focus provider prevents ambient OS focus from invalidating automated playback or pointer animation; disposal restores native input and native focus. With no provider, existing gameplay reset, pause-menu and native-pointer focus behavior is unchanged and is explicitly regression-tested.
- Independent timeline, chart editor and title fixtures now use the same input isolation as the shared gameplay context.
- Gameplay viewport projection now uses the canvas camera outside overlay mode. The pixel test renders the world and UI explicitly, avoiding Game-view visibility and WaitForEndOfFrame dependencies without skipping pixel assertions.

## Final validation

- Domain/EditMode: one final complete run, **399 passed / 399**, no failures or skips. Evidence: unity-errors-all-editmode-verified.json.
- PlayMode: the full 202-test run passed 197 and failed 5. After the last independent-fixture input-isolation changes, affected fixtures passed: SongClockResumeTests 9/9, ChartEditorScreenTests 4/4, AudioTimelineTests 3/3, MainMenuFlowTests 19/19. Latest results by unique test: **202 passed / 202**, no failures or skips. This is a full run plus affected-fixture reruns, not a single clean final full PlayMode run. Evidence: unity-errors-playmode-consolidated.json and its source run files.
- The full run passed continuous 929-note playback and automatic Results, all scene-preparation cases, the pixel-render assertion, cursor animation, gameplay health, and the new default/native focus ownership regression.
- Continuous playback measurement: wall time about 125.57 seconds; the clock stopped at completion and Results followed about 1.38 seconds later. The preserved measurement is in unity-errors-artifacts/pr-quality-remediation/final-pupa-timing.json.
- git diff --check passed. No Windows build was run because build/platform settings were unchanged and the request was to fix and verify errors.

Earlier failed and canceled runs were retained separately. They are not counted as additional unique tests. Updated test evidence was copied to unity-errors-artifacts before restoring previously tracked verification outputs.

Final editor checks: compilation successful; Console 0 errors / 0 warnings without clearing it; native focus provider restored; Play Mode stopped. Active scene: Assets/Game/Scenes/Bootstrap.unity. Evidence: unity-errors-final-console.json and unity-errors-final-editor-state.json.
