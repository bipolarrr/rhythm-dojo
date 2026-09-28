# Rhythm Dojo

For a new Windows checkout, follow [Git and Unity collaboration setup](Docs/git-unity-setup.md) to install missing tools and enable merge handling and asset/meta validation.

Unity **6000.3.23f1**, URP **17.3.0**, Input System **1.20.0**, uGUI, Test Framework **1.6.0**. No packages were added or upgraded.

Open `Assets/Game/Scenes/Bootstrap.unity` and enter Play Mode. Select a song, judgment difficulty, scroll mode and multiplier using the mouse or keyboard navigation, then choose **Load Song**. In Gameplay, **Space** starts/restarts, **D / F / J / K** play the default lanes, and **Escape** returns to the song list. Ready and Completed also have a mouse return button. Losing focus resets play to Ready.

From the song list, choose **Settings** to adjust the DSP buffer before playing. Choose Default, 32, 64, 128, 256, 512, 1024 or 2048 samples, then **Apply**. The screen reports the actual buffer size, buffer count and sample rate after Unity resets audio; supported sizes depend on the output device. Default requests the actual size captured at app startup. Failed resets attempt to restore the prior configuration and report the actual state. Back returns to the song list. The choice survives screen changes during this run and is never saved to assets, ProjectSettings or PlayerPrefs.

This controls Unity's mixer buffer and does not select ASIO or configure an ASIO driver's buffer. A Unity DSP size of 64 samples is not an equivalent test of an ASIO device buffer of 64 samples. Follow-up tests reproduced slow DSP progression without gameplay code in a standalone Windows diagnostic player, including an empty scene at 120 FPS and a memory-generated tone with no GC allocations. See `Logs/audio-performance-audit.md` for measurements, limits and diagnostic reproduction commands.

## Independent difficulty and scroll controls

Difficulty changes judgment windows only. Scroll mode and multiplier never change chart timestamps or grading.

| Profile | Perfect | Good |
| --- | --- | --- |
| Easy | ±80 ms | ±160 ms |
| Standard | ±50 ms | ±110 ms |
| Hard | ±35 ms | ±80 ms |

Easy and Hard are initial tuning values. Standard reproduces the original rules.

`ScrollSettings.asset` owns the base speed (8 units/s), reference BPM (120), and selectable multipliers (0.5, 0.75, 1, 1.25, 1.5, 2). Constant 1x is the default. Choosing another difficulty does not reset either scroll choice.

Constant scroll uses `baseSpeed × multiplier`. BPM scroll uses `baseSpeed × multiplier × BPM(t) / referenceBpm`. The scroll timeline integrates distance over every tempo segment:

```text
z = DistanceBetween(currentSongTime, noteTargetTime)
```

At default settings, 120 BPM moves at 8 units/s and 180 BPM at 12 units/s. Distance from 5s to 7s across a change at 6s is 20 units, rather than the 16 units of Constant mode. Position is continuous at the boundary; velocity changes. Holds use the same integrated head/tail distances, and their head stays on the judgment line while held.

## Content and settings

`Assets/Game/Settings/GameSettings.asset` connects the catalog, difficulty profiles, independent scroll settings, audio settings and presentation settings.

- `GameModeDefinition` owns lane count through its lane list, key bindings, labels, colors, lane spacing and HUD spacing. Session arrays, input actions, playfield geometry and HUD all derive from this definition. The product ships one four-lane mode; the structure supports other lane counts.
- `SongDefinition` contains ID, title, artist, AudioClip, chart, mode and timing. A song currently has one chart.
- `SongTiming` owns the chart/audio offset and ordered BPM points (`startTimeSeconds`, `bpm`). The first point must start at zero; BPM must be positive and finite. The first/last tempos extend before/after the map. Tempo timestamps use chart time, so the audio offset is not applied twice.
- `DifficultyProfile` owns Perfect and Good windows only.
- `AudioPlaybackSettings` owns scheduling lead and volume. Migration retains the existing 0.15s lead, rather than replacing it with the legacy script's 0.5s default.
- `GameplayPresentationSettings` owns note/playfield dimensions and HUD feedback timing/colors.

Create authored charts, songs, catalogs and settings through **Assets > Create > Rhythm Dojo**. A chart must have sorted notes, finite timestamps, valid lanes, positive hold lengths and no same-lane interval overlaps. A tap uses its start time as its end. Metadata BPM/range comes from the timing map.

The existing `RhythmGameConfig.asset` remains as a legacy migration source. Gameplay no longer reads it. Generation reads and validates legacy values once only when creating Standard, scroll, audio or the default song. Existing settings and song offsets are preserved. With no legacy asset, generation uses the new defaults, including a 0.15s scheduling lead; it does not create a legacy asset. Scheduling lead is independent of the DSP buffer and has no control in this screen.

Settings are captured when Gameplay loads. Judgment, chart, tempo and scroll calculations use immutable values; mode and presentation settings use runtime copies. Editing shared assets does not retune the active play. Return to the list and load again to apply edits. Selection is remembered for the current app run, without disk persistence.

Two generated verification songs are registered:

| Song | Chart | BPM |
| --- | --- | --- |
| Foundation Pulse | Original 12s / 18 notes / 3 holds | 120 |
| Tempo Shift | 12s / 8 notes / hold at 5.2–6.8s | 120, then 180 at 6s |

Their audio is reproducible pulse sound, not finished music. Tempo Shift's pulse changes tempo at the same boundary as its map.

## Four-person ownership

See [team ownership and integration contracts](Docs/team-ownership.md) for UI layout, UI behavior, in-game authoring, and gameplay/integration responsibilities. Layout builders now return explicit Views; screen composition injects services. Built-in assets and unsaved documents share the same playback path. The in-game editor UI and disk format remain future work.

## Architecture

| Assembly | Responsibility |
| --- | --- |
| Game.Domain | Immutable chart/tempo data, judgment settings, lane rules, session, typed events and session snapshot |
| Game.Application | Input/clock/presenter contracts, session coordinator, gameplay read model and scroll timelines |
| Game.Runtime | Unity assets, component adapters, composition, presentation and UI |
| Game.Editor | Content generation, scene generation, assembly and validation |

Domain and Application disable engine references and explicitly exclude Unity API dependencies. Runtime references Application and Domain; Editor references Runtime. Tests are separate and excluded from ordinary player builds.

`RhythmGameController` forwards Unity lifecycle calls to `RhythmSessionCoordinator`, which depends on `ILaneInput`, `ISongClock` and `INotePresenter`. `GameplayCompositionRoot` assigns the actual implementations. HUD consumes `IGameplayReadModel` snapshots and typed judgment/hold/reset events; UI formatting stays in `RhythmHudFormatter`.

`AppFlowController` owns selection and async scene transitions without a mutable global song variable. Escape requests cleanup in Update, after the input callback completes. Scene changes stop audio, dispose lane actions and unsubscribe gameplay/HUD listeners. Completed HUD retains the final song time after the clock stops.

## Timing and rules

Audio starts at `AudioSettings.dspTime + scheduleLeadTime`. Chart time is DSP elapsed time plus the song offset. Input event age is subtracted when converting Input System timestamps, preserving inputs queued during a slow frame. Music time is not accumulated from deltaTime; Dynamic input dispatch precedes the timeout sweep.

A press selects the nearest eligible Pending note in its lane within GoodWindow; near-exact ties prefer chart order. Repeated presses while held are ignored; empty presses do not penalize combo. Signed error is `inputSongTime - targetTime`. Each note contributes one final result. Miss resets combo.

Hold head grading does not increment counts. Release inside the tail window produces the worse head/tail grade; an early release misses. Still-held notes finish after end + GoodWindow using their head grade. A later release cannot rejudge them. There are no ticks or extra tail scores.

Restart resets logical state and schedules new playback. Physically held keys are blocked until released. Completion waits for the chart end, audio end in chart time, and the final late judgment boundary.

## Authoritative scenes and generation

Use **Game Tools > Scenes > Build All Scenes**, or the individual Bootstrap, Gameplay, Song Selection and Settings commands. Generated scenes are authoritative outputs of the builder.

Scene generation reads existing resources through `SceneResources`, constructs UI through the screen layout builders, and assigns dependencies through `SceneDependencyAssembler`. `SceneBuilder` controls ordering, saving and scene registration. Use **Game Tools > Content > Prepare Default and Verification Content** for initial resources or explicit verification-song regeneration; normal scene builds never regenerate song content. Default settings, test songs and presentation assets have separate preparation code.

Bootstrap → SongSelection → Gameplay → SongSelection. SongSelection also opens Settings and returns to the list. Starting Gameplay directly in the Editor uses Foundation Pulse / the configured default difficulty / Constant / 1x.

The explicit content preparation command recreates the two named test charts from code. Authored songs/charts and existing difficulty, scroll, mode and audio settings are not overwritten. Generated materials follow the presentation settings. Primitive colliders are removed. Build settings register the four game scenes first and preserve other entries. Generation checks Play Mode and unsaved scenes. Scene validation uses explicit component requirements and checks generated note views against chart kinds; note names do not affect validation. Runtime validation checks dependencies before building views for the selected song.

## Verification

The editor core / chart format v1 addition passed **99 Edit Mode tests, 10 Play Mode tests and the Windows x64 build** on 2026-09-28. See [project verification evidence](Logs/project-verification.md).

Verified on 2026-09-28 after the team-boundary refactor: **71 Edit Mode tests and 10 Play Mode tests passed**, four scenes were recreated and remained stable on repeated generation, all five full-song integration runs passed, and the Windows x64 player built successfully. Settings tests cover mouse/keyboard navigation, actual DSP configuration, Default, retained selection, guarded application and playback/restart at 256 samples. The Windows batch environment showed slow DSP time progression at 32–128 samples despite accepting those values; audible dropouts and human input/audio latency were not measured. See `Logs/team-verification.md` for refactor evidence and `Logs/settings-verification.md` for the DSP measurements. Earlier foundation evidence remains in `Logs/refactor-verification.md`.

The Unity Test Runner exposes:

- **Edit Mode:** Domain/Application tests plus Unity assets, migration/defaults/preservation, explicit scene requirements, lane layout, hold rendering and repeatable generation.
- **Play Mode:** Actual mouse click, keyboard UI navigation, DSP buffer application and measurement, restart held-key blocking, focus loss, Escape cleanup, two-song transitions and a six-lane mode across input/HUD/playfield.

Results are written to `Logs/settings-editmode.xml` and `Logs/settings-playmode.xml` when run with the commands below.

**Game Tools > Verification > Verify Foundation (Play Mode and Player Build)** additionally regenerates scenes, exercises full timestamped keyboard runs through Bootstrap, checks the deliberate 180ms hitch, and builds `Builds/Windows/RhythmDojo.exe`. It checks the original 13 Perfect / 2 Good / 3 Miss run, restarted 18 Perfect run, no-input 18 Miss run, and Tempo Shift's 8 Perfect runs in both Constant and BPM modes. Evidence goes to `Logs/verification.txt`, the Unity log and `Logs/playmode.png`.

The input checks temporarily adjust only in-memory focus behavior for batch mode and restore it. They do not establish human keyboard/audio latency.

Close any Editor using this project before running batch checks:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -runTests -testPlatform EditMode -testResults 'C:/Users/song/RhythmDojo/Logs/settings-editmode.xml' -logFile 'C:/Users/song/RhythmDojo/Logs/settings-editmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -runTests -testPlatform PlayMode -testResults 'C:/Users/song/RhythmDojo/Logs/settings-playmode.xml' -logFile 'C:/Users/song/RhythmDojo/Logs/settings-playmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -executeMethod RhythmDojo.EditorTools.FoundationVerification.RunAll -logFile 'C:/Users/song/RhythmDojo/Logs/settings-foundation.log'
```

Wait for each Editor process to exit before launching the next. Do not add `-quit` to test or asynchronous integration runs.

## Deferred performance work

Views are created for the whole small chart when Gameplay loads; judgment still scans the chart. No pooling, lane cursor or active-range optimization is claimed.

When an authored full song is available, profile a Windows Development Build before adding lane/time cursors, distance-based active ranges and tap/hold pools. Active ranges must use the scroll timeline, including BPM changes, and preserve visible hold bodies. Keep the linear implementation as a result/event-order oracle and target zero steady-state GC allocations in judging and note rendering.

The headless editor core supports multiple charts per song, transactional editing, 100-command Undo/Redo, UTF-8 JSON v1 storage, content-hashed audio import and conversion to the existing playback document. See [the file format and API guide](Docs/chart-format-v1.md), [JSON Schema](Docs/rdchart-v1.schema.json) and [examples](Docs/examples).

Tempo ramps, stops, reverse scroll, separate SV events, the chart editor UI, user-song catalog registration, external audio decoding, pause, calibration UI and saved records remain future work.


