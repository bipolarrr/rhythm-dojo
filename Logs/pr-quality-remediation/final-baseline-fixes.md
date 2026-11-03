# Final baseline fixes

Base: d675507. Source only; root executes Unity and records results.

- ResultsFlowTests retained its existing name but now verifies Escape pauses and shows no Results, then clicks the pause exit button and checks selection/empty LastResult. The prior immediate-selection expectation contradicted the pause feature.
- SongRowsKeyboardSelectionUpdatesSelectedSong now filters favorites to the two fixture songs before physical keyboard navigation. GameplayTestContext clears/restores selection preferences; personal song files remain untouched. The exact expected song and index assertions remain.
- Three legacy PupaResultsFlowTests no longer skip on missing personal WAV. TemporaryWaveFile generates 131.54s mono 44.1kHz 16-bit PCM, independent onset 0.5033333333333334s, 440Hz tone at amplitude 0.15. Each test calls the real external decoder and disposes lease before file. Existing 929-note, first attack, timestamped input, full duration and tail assertions remain. Legacy names/category are preserved; these are generated-audio regressions, not commercial PUPA-audio verification.
- FoundationVerification.BuildPlayer includes Results, matching gameplay navigation and registered scenes. RunAll already selects EditMode plus PlayMode when building; no orchestration change required.

Planned root verification: affected ResultsFlow method, SongRowsKeyboardSelection method, PupaResultsFlowTests (7 tests including one full-song run), then final Foundation including Windows build. Independent review remains required. No Unity executed or passing result claimed here.
Root validation: Results regression 1/1, selection keyboard regression 1/1, PupaResultsFlow 6/6 with zero skips (including full real-time external WAV playback). Refreshed/recompiled first; Play Mode exited and Editor ready between suites. Independent read-only review found no blockers or weakened assertions. Build scene correction will be exercised by final Foundation.
