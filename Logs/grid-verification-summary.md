# Audio timeline and GRID verification

Implementation uses additive Domain/Application helpers and Runtime/Authoring components. Existing code changes connect shared timing, fractional note snap, optional JSON metadata and menu initialization. Existing public APIs and asset names are preserved; ChartBeatGrid.SetNotes(TempoMap, ...) remains available.

## Results

- Unity 6000.3.23f1 compiled the project.
- Full foundation EditMode run: 176/176 passed. Final affected EditMode rerun: 41/41 passed, including three further regression tests. Across these runs, 179 unique EditMode cases passed.
- Foundation PlayMode batch run: 85/86 passed. The remaining existing GameplayWindowRendersJudgmentLineBelowCenter test uses WaitForEndOfFrame, unsupported in batch mode. A normal Editor run passed this case together with all 11 affected authoring cases (12/12).
- Final authoring rerun after all source changes: 11/11 passed.
- Across the normal and batch runs, all 86 unique PlayMode cases passed. The batch Foundation command itself is not reported as a clean pass because of the existing batch-only restriction.
- New asset .meta pairing and source whitespace checks passed.
- Windows build result is recorded below after the final build exits.

## Coverage

Timing round trips across tempo changes and long durations; compound meters; whole-grid movement across tempo/meter changes; both note retention policies, hold endpoints, IDs and atomic undo/redo; legacy/default JSON and extended JSON round trips; malformed timing validation paths; stereo/transient/final-block waveform peaks; actual WAV import; DSP preview playback and pause; seeking and pointer-anchored zoom; advanced segment/meter editing; dropdown readability; resizable waveform; signed grid origins; original note placement/deletion/holds/scrolling/playtest return; small and large rendering resolutions; far-end overview grid and triplet subdivisions.

## Scene verification side effects

The initial full EditMode suite includes existing ContentTests.GeneratedScenesRemainStableAndSettingsAreNotOverwritten, which calls SceneBuilder.BuildAll and rewrites generated scenes. This occurred before the scene backup was created, so the initially dirty scenes are not restored byte-for-byte to their initial serialization IDs. Subsequent verification uses Logs/grid-validation-backup to preserve scenes, materials, settings and project settings. The initially clean Gameplay scene and materials were restored to their original Git contents. Existing Assets/_Recovery files were preserved.

The feature itself is generated at runtime when ChartEditorMenu initializes and does not require scene regeneration.

## Evidence

- Logs/verification-editmode.xml
- Logs/verification-playmode.xml
- Logs/grid-final-editmode.xml
- Logs/grid-final-playmode.xml
- Logs/grid-final-authoring.xml
- Logs/grid-build.log
- Logs/grid-timeline.png
- Logs/grid-timeline-advanced.png
- Logs/grid-timeline-dropdown.png
- Logs/grid-timeline-resized.png
- Builds/Windows/RhythmDojo.exe

Usage: Docs/editor-audio-timeline.md.

Final Windows build: PASS, process exit code 0. Scene/settings/material backup hashes match after restoration.

Not separately verified: individual OGG/MP3 files and subjective listening quality; real WAV import/PCM peaks/DSP transport were exercised.

## Follow-up verification: RGB waveform and simplified GRID controls

- RGB waveform analysis: 5/5 EditMode tests passed with Unity 6000.3.23f1 in an isolated verification project. Covers low/mid/high frequency colors, opposite-phase stereo, local frequency changes, silence, cache rebuild and original peak preservation. Results: Logs/rgb-waveform-tests.xml.
- Final editor integration: 11/11 PlayMode tests passed in a copy of the project using Unity 6000.3.23f1. Covers waveform import, seeking, zoom, DSP playback, leftmost visible grid origin instead of playback cursor, lock protection, note preservation, simplified button layout, existing chart/menu/rendering behavior and retained tempo/meter editing internals. Results: Logs/grid-ui-final-tests.xml.
- UI screenshot inspected: Logs/grid-ui-updated.png. The controls end with Lock followed by Note Snap; GRID Undo, Redo and Advanced buttons are removed. The origin button is labeled '그리드 수정'.
- These follow-up runs used isolated project copies and did not regenerate the working project's scenes. Temporary integration project was removed after verification.
- The Windows build noted above predates these follow-up changes; no new player build was made for them.

## Follow-up: entered BPM was ignored by Grid Correction

- Reproduced with a regression test before the fix: entering 128 in Grid BPM and clicking '그리드 수정' left the saved tempo at 120. Results: Logs/audio-sync-repro.xml (expected failure, Expected 128 / Actual 120).
- Grid Correction now applies the entered BPM and the leftmost visible grid origin in the same edit. The existing note times and undo behavior remain covered by the regression test, including playback-document tempo checks at 0, 30, 90 and 150 seconds.
- Verified the user-provided Jotori / Saiki WAV locally: 44,100 Hz, stereo, 7,531,454 frames, duration 170.781269841 seconds. Its onset phase remains consistent with the specified 128 BPM across 10–160 seconds. A beat is 0.46875 seconds; the source file was not modified or added to the repository.
- Final Unity 6000.3.23f1 PlayMode run: 13/13 passed (12 editor integration/regression tests plus one local real-file verification fixture). Results: Logs/audio-sync-final.xml. The local fixture is Logs/SaikiPlaybackVerification.cs.
- Actual preview audio sample time versus DSP-derived editor clock after seeks to 0, 30, 90 and 150 seconds differed by at most 0.014 milliseconds in the test run. Details: Logs/saiki-playback-verification.txt. This measures internal playback/clock agreement, not physical speaker or display latency.
- Verification ran in an isolated copy of the project; working scenes and the original audio were left untouched. No player rebuild was performed for this fix.
