# Move selection loading before Play — 2026-10-31

## Behavior

Title composition asks AppFlow to prepare SongSelection additively. The selection canvas actually renders behind the opaque title at sorting order -10000, allowing fonts, glyph meshes, wood text materials and row UI to warm before Play. Its screen controller, raycasters, event system, camera and audio listener remain disabled; a parent CanvasGroup also blocks input. Composition still initializes the disabled screen explicitly, restores title focus, and settles canvas rebuilds across two frames before marking preparation ready.

Play reuses that same loaded scene. It restores the prepared output/input components, immediately hands input blocking and panel hiding to the entrance transition, restores canvas ordering, makes selection active, and asynchronously unloads Title. No selection scene activation or initialization occurs on the normal Play click. A Play request arriving during preparation waits for that preparation rather than issuing another scene load. Other destinations clear the preparation state; the existing no-title-view Bootstrap start remains functional.

The earlier door/wood-face choreography is unchanged in this change.

## Measurement

Same Editor, 60 FPS target, actual Play → selection flow:

| Metric | Previous click-time loading | Prepared scene reuse |
| --- | ---: | ---: |
| Maximum measured frame interval after click | 50.773 ms | 21.897 ms |
| Click-test return to first door motion, including the 0.15-second menu dismissal | 246.615 ms | 185.059 ms |
| Maximum frame interval during door movement | 17.501 ms | 18.079 ms |

The previous roughly 51 ms scene-loading frame no longer occurs after Play in this run. This does not claim every frame on every machine is below 16.7 ms. Raw evidence: `Logs/door-transition-click-response-after.csv` (before) and `Logs/door-transition-preloaded-profile.csv` (after).

## Verification

- Compilation and source whitespace checks passed.
- New title-preparation test passed: hidden screen, lower canvas order, blocked input, retained title focus, one active audio listener, and working title menu.
- Frame-profile regression passed with selection prepared before clicking.
- MainMenuFlowTests initial full run: 17 passed, 1 assertion failed because Unity's scene handle value was compared to an int without an explicit cast. The assertion was corrected. Targeted rerun of scene reuse, Bootstrap fallback and editor navigation: 3 passed, 0 failed (`Logs/verification-selectionpreparation.xml`, start 2026-10-31 14:47:51Z).
- Scene reuse regression confirms no SceneLoaded event after Play and the identical prepared scene handle, followed by title unload and exactly one remaining event system/audio listener.
- Final Verify Smoke Actions: 4 passed, 0 failed (`Logs/verification-smoke.xml`, start 2026-10-31 14:48:55Z).
- No full-project suite or Windows build: scope was title preparation, transition and shared navigation.
