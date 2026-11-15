# In-game presentation

Gameplay composition remains in `GameplayHudLayoutBuilder`, `PixelGameplayStyle`, and
`GameplayFeedbackBuilder`. Regenerate with **Game Tools > Scenes > Build Gameplay**.
The world camera and note/judgment projection are unchanged. The backdrop is a separate
non-interactive canvas; the existing viewport composites the world over it.

The song panel binds the current request's title and difficulty and the matching library
entry's cover. Missing covers use the existing selection-background fallback. Album
images preserve aspect ratio. Legacy statistical widgets retain their serialized
references for existing consumers but are inactive, including the obsolete focus toggle.
The vertical gauge uses SessionSnapshot.HealthRatio; its damage trail lasts 240 ms.
Its horizontal position follows the projected near edge of the selected mode's lanes,
so the gap remains small when the mode or display aspect changes.
Judgment labels and pixel impact bursts remain event-driven and fade out.

## Startup and ownership

`GameplayCompositionRoot` waits for audio preparation and scene transition completion,
then shows the HUD's key labels and short Escape hint. The two-second interval starts
after the first display frame. SongClock's existing readiness contract blocks manual
starts during the introduction. At the end the guide objects are deactivated and the
existing coordinator schedules both song and chart on the DSP clock. Audio offset,
lead-in, input calibration, note travel and judgment rules are unchanged. A device
reset during introduction repeats preparation before starting. Restart does not revive
the guide; scene re-entry creates a new introduction. Leaving cancels the coroutine.
The existing three-second *pause resume* countdown remains separate from initial entry.

## Original assets

- `Assets/Game/Art/DojoGameplay/TrainingHall.png`: built-in imagegen output, using
  `Art/DojoRenewal/PracticeHall.png` as the visual reference. Point filtering, no mipmaps,
  no texture compression. Original generated output is retained under
  `C:/Users/song/.codex/generated_images/01a1cd26-093b-79a0-8a4e-a9644f3e570c/`.
- Gameplay effects now use individually verified CC0 recordings. `StrikeWhoosh.wav`
  and `StrikeImpact.wav` retain their paths for serialized references; active gameplay
  chooses multiple takes from `DojoSoundBank`. The old generator entry points now run
  `Tools/prepare_recorded_audio.py`.

See [sound design and source licenses](sound-design.md) for the recording URLs,
processing recipes, current cue timing, memory budget, and verification limitations.

### Background generation prompt

Use case: style-transfer. Create a new widescreen 16:9 gameplay backdrop extending this
exact Korean hanok martial arts training hall art direction. Reference is
style/material/palette reference, not a UI. Crisp detailed 16/32-bit pixel art, dark warm
walnut beams and weathered amber grain, subdued parchment plaster, muted teal roof tiles,
restrained hard edged pixel shadows. Frontal symmetrical interior training hall, floor
receding toward rear wall, side openings to small quiet mountain garden. Center 45
percent of width must remain very dark, uncluttered low-contrast wood and wall, to support
bright falling rhythm notes; no pillars or furniture crossing center. Keep ceiling within
top 12 percent. Left 22 percent quiet dark wall to support album art HUD. Sparse side
architectural detail. No people, no training dummy, no hanging ropes, no text, no UI
panels, no gauges, no notes, no neon, no bloom or blur. Polished finished game background,
consistent with attached reference's pixel density and materials.

## Scene routes

Build settings register Title → ModeSelection; practice/challenge use the same
SongSelection scene and enter Gameplay through AppFlowController. Bootstrap initializes
that same selection scene. Editor full playtests also enter that Gameplay scene; the
embedded chart-editor viewport is an editor tool. Scene loading into gameplay is Single,
so prior menu canvases are unloaded. PreparedSelectionScene only preloads selection.
Tests use these product scenes; browser fixtures intentionally choose the current list
view, which remains available from Challenge and Practice's song-list action.

Verification evidence is recorded in `Logs/ingame-presentation-verification.txt`.

## Settings, results and pause menu renewal

`SettingsLayoutBuilder` generates both the direct Settings scene and the reusable
`Resources/UI/SettingsPopup.prefab` used by AppFlowController. The popup shows the new
preparation-room backdrop during its existing open/close animation. Results uses a
separate record alcove; gameplay retains the existing TrainingHall art. Both new images
were generated with the built-in imagegen tool using PracticeHall and ScribingRoom as
palette, material and pixel-detail references. The saved sources are
`Assets/Game/Art/DojoRenewal/PreparationRoom.png` and `RecordAlcove.png`.
The [generation prompts](../Logs/dojo-menu-refresh/art-prompts.txt) record both requests.

`DojoScreenStyle` authors brush-text actions, ink slider rails, option values and their
focus strokes. It replaces the old rectangular SettingsControl button surfaces in these
screens. `DojoControlFocus` animates those serialized views from the existing EventSystem
selection. Deliberate pointer movement can select a control; closing a dropdown under a
stationary pointer does not steal keyboard focus. Key-binding rows scroll into view when
selected. Fixed presentation objects remain in the builders and saved scenes/prefab.

The existing InputSystemUIInputModule uses persistent action references from the shared
input asset. Settings, Results and Gameplay use UI/MenuSubmit for Enter and Space;
Title/ModeSelection retain their existing submission and strike controls. Up/down moves
between controls, left/right adjusts sliders and closed option lists, and Esc closes the
innermost menu. ModalInputScope restores the previous screen's selection. Space is
reserved for taps during calibration and speed testing, then restored for menu submission.
Gameplay Esc continues to open pause, and settings remain unavailable during a song.

Current menu verification and captures are recorded in
`Logs/dojo-menu-refresh/verification.txt`.

The existing **Game Tools > Preview > Results** and **Results (Game Over)** commands
replay the authored entrance after the sample result has been displayed. While that
preview is running, either command can be selected again to restart the motion or switch
the sample outcome. **Game Tools > Preview > Mock Controls** also edits the summary
values live and opens the authored pause panel. Results uses `ResultsPresentation`;
pause uses its existing `DojoReveal`. The editor host skips game composition and reuses
loaded views. It temporarily skips domain reload, restores the user's Play Mode options
and start scene on exit, and never connects records or persistent settings. See
[the preview workflow](verification.md#플레이-없이-결과-ui-미리보기).
