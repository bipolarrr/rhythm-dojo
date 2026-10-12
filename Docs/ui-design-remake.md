# Modern dojo UI and gameplay feedback

The title, song selection, settings, and gameplay HUD now share dark panels, cream text, red accents, and cyan feedback. Gameplay separates song information and judgment statistics from large combo and timing feedback, keeping the note highway clear.

A custom native cursor adds hover feedback, click ripples, and click audio. Scene cameras now retain an active audio listener through title, selection, settings, and gameplay transitions. Successful notes add a short synthesized hit sound, pooled lane bursts, and a small camera recoil; hold notes keep a glow until resolution. Chords share one attack sound per frame, and reset/scene return clears effects and restores the camera. Scoring and judgment rules are unchanged.

Lasting layouts live in the scene builders. Regenerate scenes with **Game Tools > Scenes > Build All Scenes**.

## Validation

- Unity 6000.3.23f1 compilation succeeded.
- GameplayHudFlowTests: 4 passed; DojoClickFeedbackTests: 3 passed; GameplayHitFeedbackTests: 3 passed.
- Hit tests verify actual audio output, silent misses, bounded pools, hold completion, camera restoration, rebinding, and scene cleanup.
- Windows standalone build succeeded after the hit feedback changes.
- Unity meta validation passed (218 staged GUIDs).
- The latest Foundation run ended with 117 EditMode tests passed and one timeout in SceneGenerationDoesNotWriteContentAssets while regenerating scenes in the live Editor. It did not proceed to the full PlayMode suite. The earlier complete Foundation run before hit feedback passed 118 EditMode and 45 PlayMode tests. Full Foundation verification still needs a rerun.

## Screenshots

![Title](ui-design-remake/title.png)
![Settings](ui-design-remake/settings.png)
![Gameplay](ui-design-remake/gameplay.png)
![Hit feedback snapshot](ui-design-remake/hit-feedback.png)

## Health and game over

The gameplay HUD shows health above the judgment feedback. Each run starts with 100 health: Perfect recovers 2, Good recovers 1, and Miss costs 10, clamped to 0–100. Holds affect health once when resolved. The gauge turns gold at 50 or lower and red at 25 or lower.

At zero health, the session enters GameOver, a centered GAME OVER overlay appears, and the song clock and audio pause. Remaining notes stop moving and receiving judgments. Space restarts with full health; Escape returns to song selection. Failed runs are not saved as completed records.

Completing the entire chart shows a centered cyan GAME CLEAR overlay. Space restarts and clears the overlay; Escape or the song selection button returns to the song list. GAME OVER remains reserved for zero health.
