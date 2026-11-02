# Door and logo synchronization — 2026-10-31

The subsequent click-time loading fix and current measurements are recorded in `Logs/selection-preload-verification.md`. Selection is now warmed behind Title and reused on Play.

The requested choreography keeps the expanded logo stationary while the doors move first. When each moving door seam reaches its corresponding logo split edge, both continue together with their edges aligned. Moving the logo from the first frame was an incorrect interpretation and has been replaced.

Doors follow one uninterrupted 1.4-second smootherstep curve. Contact is determined by geometric overlap, without changing door speed or restarting the animation. The previous 36-unit catch-up was too short perceptually and used the front artwork seam rather than the visible inner edge of the cut face. Contact now begins at the 12-unit wooden cut face and blends over 96 canvas units. The quintic matches position, velocity and acceleration at the endpoints, keeps contact offset within the cut-face thickness, and then follows the door exactly. A plaque without depth retains the shorter fallback blend. Menu expansion/reversal, loading readiness, audio start, doorway clipping, split-face depth and approach timing remain in place. `DoorContactProgress` remains as a compatibility timing marker and no longer controls door movement.

The title now composes the existing AppFlow and opens SongSelection directly, avoiding the redundant Bootstrap scene activation/unload. Starting without a title view retains the Bootstrap route. The existing test method name `StartLoadsBootstrapThenExistingSongSelection` is preserved, while its assertions now verify the direct title route. Unchanged logo separation no longer dirties/rebuilds its mesh every frame.

Follow-up correction: Play still had a hard-coded 0.25-second idle wait when its button had no board-break animation. The current vertical menu uses this path. That wait is now skipped for Play, so dismissal begins during the click callback itself. Other buttons retain their board/action timing. AppFlow song discovery and audio/graphics initialization now happen in title composition before music/interaction, rather than on the click.

Entrance-specific selection loading is additive: the title camera stays available behind the overlay, while its event system/audio listener are disabled before selection activation. Once selection has loaded, it becomes the active scene and the old title is unloaded asynchronously. The entrance waits for both selection readiness and that cleanup before starting door movement. This avoids automatic unused-asset collection during a single-scene replacement; other navigation continues to use its existing loading path.

## Smoothing measurements

Same Editor, 60 FPS target, actual Play click → SongSelection flow:

| Measurement | Before | After additive entrance loading |
| --- | ---: | ---: |
| Time from click-test return until first door motion, including menu dismissal | 1064.412 ms | 514.436 ms |
| Maximum frame interval across the measured transition | 291.893 ms | 53.266 ms |
| Maximum frame interval while doors move | 18.145 ms | 18.647 ms |

These are measurements from the captured Editor runs, not fixed timing guarantees. Evidence: `Logs/door-transition-smoothing-before.csv` and `Logs/door-transition-smoothing-after.csv`. Readiness profile regression passed with opening zero at selection activation.

Latest follow-up run after removing the idle wait and warming AppFlow: first door motion at 246.615 ms (previous 514.436 ms), maximum transition frame 50.773 ms, maximum moving frame 17.501 ms. Menu feedback now begins immediately rather than waiting for the door clock. The scene activation frame still takes about 51 ms in this Editor measurement; this is not a claim that all loading stalls are eliminated. Evidence: `Logs/door-transition-click-response-after.csv`.

Verification in Unity 6000.3.23f1:

- Compilation completed without errors.
- The updated regression finds actual contact from the moving edges and verifies nonzero, continuous door speed on both sides of contact and the former pause point. It also checks stationary logo halves before contact and aligned plaque/logo motion afterward.
- Latest MainMenuFlowTests: 17 passed, 0 failed (49.69 seconds). Includes a new immediate-dismissal regression, continuous door movement, cut-face contact easing, readiness, timeout recovery, title cleanup, exactly one event system/audio listener and actual Title → SongSelection navigation.
- Latest Verify Smoke Actions: 4 passed, 0 failed. XML: `Logs/verification-smoke.xml`, start 2026-10-31 14:10:27Z.
- Rendered and inspected contact at 35%, 41% and 47%, with additional samples at 38%, 44% and 50%. Evidence: `Logs/door-contact-review-*.png`.
- Rendered opening samples at 0%, 20%, 42%, 60%, 100%; visually inspected approach and coupled movement. Contact is geometry-dependent, not fixed at 42%. Evidence: `Logs/door-logo-sync-{20,42,60}.png`, generated by `Logs/RenderDoorLogoSync.cs`.
- Source diff whitespace check passed. No player build or full-project test run; scope is entrance animation and shared navigation.
