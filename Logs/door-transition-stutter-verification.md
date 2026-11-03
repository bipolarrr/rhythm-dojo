# Door transition stutter investigation

Environment: Unity 6000.3.23f1 running Editor, requested 60 FPS. Measurements are frame intervals from the actual Title → Bootstrap → SongSelection flow, not a synthetic easing calculation.

## Reproduction

The door animation began immediately, concurrently with Bootstrap activation, project song discovery, SongSelection activation and UI construction. The initial SceneLoadingCompletesBeforeDoorAnimationStarts regression test failed: doors were already 25.18% through their animation clock when SongSelection activated. Maximum frame interval while doors were moving was 293.507 ms. Raw baseline: door-transition-frame-profile-before.csv.

## Fix

DojoEntranceTransition now holds the closed cloned doorway while waiting for SelectionReady. After scene/UI initialization it settles canvas/font work across two frames, then starts the door animation and audio. The 1.4-second smootherstep motion, logo/door displacement relationship, input blocking and entry zoom are preserved. Readiness timeout recovery remains available while waiting.

## Targeted verification

The same regression passed after the fix. Door opening at selection activation: 0. Maximum moving frame interval: 17.996 ms on the first corrected run and 19.232 ms on the final corrected run. Raw final post-fix capture: door-transition-frame-profile-after.csv. These are measurements from this Editor run, not a guarantee for every machine or frame.

The alignment and readiness tests now supply/await the selection readiness signal explicitly. The regression records frame timing and verifies scene activation completes before visible motion; it does not impose a machine-dependent FPS threshold.

## Broader verification and recovery correction

MainMenuFlowTests initially completed 15/16 successfully; the timeout test exposed that readiness timing began before deferred clone cleanup and immediate timeout configuration completed. Reveal now first yields one frame for copied menu/input destruction, then begins its readiness wait. The failed timeout recovery test passed on targeted rerun. Successful broader coverage includes logo attachment, readiness/input restoration, actual scene entry, settings restoration, editor entry, quit confirmation and the frame-profile regression.

Final corrected frame-profile regression: passed. Final Verify Smoke Actions: 4 passed, 0 failed, 0 skipped, 0 inconclusive. Source whitespace checks passed. No player build or full-project suite was run: verification focused on the changed entrance sequence and shared navigation smoke coverage.
