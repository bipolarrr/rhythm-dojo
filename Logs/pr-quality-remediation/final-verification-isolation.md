# Final verification isolation and time-domain correction

Base: 76b6477. First full Foundation: EditMode 373 passed; PlayMode 155 passed, 1 failed, 0 skipped. Root preserved full reports and leaked-asset diff before restoring generated assets. No product/runtime changes in this correction.

## Full-song timing

RealPupaAudioFinishesAndAutomaticallyLoadsResults failed at 125.5693146 wall seconds against float clip length minus six seconds (125.539993). That wall interval began before the scheduled audio start. SongClock schedules 0.15 seconds ahead, then follows DSP progress; wall time and audio time can diverge over the full song. Chart last note is 122.8666667 seconds, Sparring Good window .05, chart completion approximately 123.9166667, audio offset -.02, followed by a one-second real-time outcome display and scene transition. The first failure did not record enough timing information to prove which contribution caused the 29 ms excess. It is not attributed solely to pre-roll or device drift.

The unchanged six-second tail guarantee is now measured in DSP seconds from the captured scheduled audio anchor, against sample-exact clip duration. The wall deadline remains chart completion plus 15 seconds. The lower musical-time bound, resolved-note count, Completed state, 929 notes and destroyed SongClock checks remain; a stopped-clock observation at Completed is added.

A JSON record at Logs/pr-quality-remediation/final-pupa-timing.json contains start, scheduled anchor, first Completed and Results DSP/wall timestamps; sample count/rates; DSP buffer configuration; maximum observed frame gap; wall/audio elapsed values; and the completion-to-Results interval. Completion is bracketed by the last Playing and first Completed observations. The outcome-delay interval must be consistent with at least one second (upper endpoint >=1); this does not prove its entire interval is >=1. Existing ResultsFlow tests independently verify the visible outcome delay. Measurements are saved before final result assertions, including failed tail checks.

## Builder-test isolation

The previously fixed SceneRegenerationTests restores all seven scenes and SettingsPopup. Two other tests call BuildAll twice without restoring generated assets: ContentTests.GeneratedScenesRemainStableAndSettingsAreNotOverwritten and TeamBoundaryTests.SceneGenerationDoesNotWriteContentAssets. Root observed all seven scenes and SettingsPopup altered during the full run, consistent with those calls.

Both now acquire a test-only GeneratedSceneSnapshot scope containing exact bytes for seven scenes, popup and their metas, plus build settings. Scope disposal closes generated scenes, restores bytes, refreshes imports and reopens Bootstrap, including on assertion failure. Existing test names and assertions are unchanged. SceneRegenerationTests retains its existing local backup to minimize restructuring and preserve names. No content assets or production builders are modified.

Root validation pending: three builder fixtures with sixteen-file hash comparisons, full Pupa fixture, independent review, then repeat full Foundation. No Unity runs or commits performed by this agent.

Root validation: SceneRegeneration 2/2 at preceding commit 76b6477; Content and TeamBoundary builder tests each 1/1 after this correction, all sixteen hashes unchanged. PupaResultsFlow 6/6 passed without skips; scheduled-anchor audio elapsed 125.332667 seconds, wall elapsed 125.463869 seconds, exact clip duration 131.54 seconds. Independent review found no outstanding issues. Editor left Play Mode and returned ready. Full Foundation repeat remains pending.
