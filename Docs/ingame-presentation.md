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
- `Assets/Game/Audio/Gameplay/StrikeWhoosh.wav`: 200 ms, 48 kHz mono 16-bit PCM.
- `Assets/Game/Audio/Gameplay/StrikeImpact.wav`: 135 ms, 48 kHz mono 16-bit PCM.
- `Tools/generate_strike_audio.py`: deterministic offline synthesis source, Python
  standard library only. Run `python Tools/generate_strike_audio.py` from the repository.
  The WAVs are the lossless source masters and the runtime assets.

The whoosh combines independently seeded air noise, a moving low-pass band, a pressure
envelope and filtered cloth texture. The impact excites three damped low-frequency modes
with a noise impulse and adds a short filtered skin attack. Both have click-preventing
edge tapers and no reverberation. Peak/RMS levels before Unity source gain are
whoosh -4.0/-22.3 dBFS and impact -3.0/-18.9 dBFS. Import uses preloaded, decompressed PCM.
Input gain is .24 and impact source gain .25. The existing AudioListener master volume
applies to both. Input and judgment play independently; chords produce one voice per
frame per sound to avoid multiplying chord loudness. Misses do not play impact audio.
Sources, burst pools and clips are authored; audio playback does not synthesize or
allocate game objects at runtime.

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
