You are starting a real Unity rhythm game project, not making a disposable tech demo.

Environment:

* Unity 6.3 LTS
* C#
* 3D project
* URP
* Desktop PC is the initial target
* 4-key rhythm game
* Visually rendered in 3D
* Gameplay logic is mathematically 2D / lane-and-time based
* Desktop keyboard input is the initial input method

The initial control scheme may use:

D F J K

for lanes 0, 1, 2, 3.

The important architectural principle is:

3D is presentation.
Lane + song time is gameplay.

Do NOT make world-space physics authoritative for rhythm gameplay.

Your job is to establish a clean, playable, extensible rhythm-game foundation.

Do NOT attempt to build a complete rhythm game.
Do NOT add speculative features that were not requested.
Do NOT introduce large frameworks simply because they might become useful later.
Do NOT silently install third-party packages.
Do NOT modify ProjectSettings unnecessarily.

First inspect the existing Unity project and installed packages.

Work with the project as it actually exists rather than assuming APIs, packages, or settings.

# CORE GAMEPLAY MODEL

The authoritative rhythm-game state must NOT be based on physical collision.

A note should fundamentally be represented by gameplay data such as:

* lane index
* note kind
* start time
* end time for a hold note

For this initial version, implement:

* tap notes
* hold notes (also called long notes)

Do not add hold ticks, variable tick scoring, release effects with separate score systems, or other elaborate hold-note mechanics. A hold note only needs a start, a sustained interval, and an end judgment.

Do not implement:

* sliders
* flick notes
* mines
* modifiers
* multiplayer
* replay systems
* online scoring
* chart editors
* procedural charts

unless something already in the project explicitly requires them.

A tap note has one target time. A hold note occupies the interval from its start time through its end time. Require `endTime > startTime` for holds. Do not infer note kind or duration from the visual object's scale.

A note's actual gameplay position is defined by its chart time.

Its rendered 3D position should be derived from something conceptually equivalent to:

distanceFromJudgementLine =
(note.startTime - currentSongTime) * scrollSpeed

The precise world axis may differ depending on presentation, but the principle must remain the same.

Do NOT determine hit timing by asking whether a note GameObject physically reached a collider or trigger.

Do NOT determine misses using OnTriggerEnter / OnTriggerExit.

Do NOT use Rigidbody movement as the authoritative note clock.

Do NOT use frame count as musical time.

GameObject movement exists only to visualize rhythm-game state.

# TIME MODEL

Rhythm-game timing must use a dedicated song clock.

Create a small runtime SongClock abstraction.

It must expose current song time with sufficient precision for rhythm-game judgment.

Prefer Unity audio DSP timing such as AudioSettings.dspTime where appropriate rather than treating Time.time or Update frame count as authoritative audio timing.

The relationship between:

* audio start
* DSP time
* gameplay song time

must be explicit and readable.

For example, conceptually:

songTime = AudioSettings.dspTime - scheduledSongStartDspTime

The exact implementation may account for offsets where necessary.

Use double precision for authoritative musical timestamps where practical.

Do not repeatedly accumulate deltaTime to derive song position.

The song clock must remain runtime code and must not depend on UnityEditor.

# GAMEPLAY SPACE

Although the game is visually 3D, model gameplay as four discrete lanes.

Conceptually:

lane 0
lane 1
lane 2
lane 3

The rendered playfield may place those lanes along the X axis.

Notes may visually travel along another world axis toward a judgement line.

However:

* lane identity is not determined by world position
* hit timing is not determined by world position
* note state is not determined by physics

The note's data determines its lane and timing.

The transform is merely a projection of that state into the 3D presentation.

# CRITICAL ARCHITECTURAL RULE: GENERATED SCENES

Scene contents must be authored primarily through C# Editor tooling.

Do not manually construct the final Bootstrap or Gameplay scene as the authoritative source of truth.

Instead:

* implement Editor-only C# scene builder scripts
* expose clear buttons/menu commands in the Unity Editor
* pressing those commands must construct or reconstruct the required scenes
* save the resulting `.unity` files
* generated `.unity` scenes are products derived from C# scene-building logic

The C# scene builders are the authoritative definition of these scenes.

A developer must be able to delete the generated scenes and regenerate equivalent scenes through the provided Editor command.

Scene generation must be idempotent enough that repeated execution does not accumulate duplicate objects or stale configuration.

Prefer clean scene reconstruction over attempting to patch arbitrary existing scene contents.

# STRICT EDITOR / RUNTIME SEPARATION

Runtime game code must not depend on Editor-only functionality.

Runtime code MUST NOT reference:

* UnityEditor
* UnityEditor.SceneManagement
* AssetDatabase
* EditorWindow
* EditorGUILayout
* MenuItem
* Handles
* PrefabUtility
* EditorApplication
* other Editor-only Unity APIs

Do not hide fundamental Editor dependencies behind:

#if UNITY_EDITOR

inside gameplay classes.

Prefer physical separation.

Use a structure similar to:

Assets/
Game/
Art/
Audio/
Materials/
Prefabs/
Scenes/
Scripts/
Runtime/
Audio/
Core/
Gameplay/
Input/
Presentation/
UI/
Editor/
SceneGeneration/
TestContent/
Settings/

Everything under Scripts/Editor is tooling.

Everything under Scripts/Runtime must remain standalone-player compatible.

Create assembly definitions if appropriate:

Game.Runtime.asmdef
Game.Editor.asmdef

The allowed dependency direction is:

Game.Editor
↓
Game.Runtime

Never:

Game.Runtime
↓
Game.Editor

If asmdefs are introduced, configure them so that this rule is enforced by compilation rather than convention alone.

# PROJECT FOUNDATION

Create the minimum foundation required for a playable four-key rhythm-game loop.

This is intentionally a cohesive one-shot foundation. Tap notes and hold notes are both baseline rhythm-game concepts and belong in this implementation. Keep their rules small and explicit instead of deferring hold notes to a disconnected later pass.

Create:

* Bootstrap scene
* Gameplay scene

Bootstrap should be extremely small.

Gameplay should contain the actual rhythm-game test environment.

Do not create an elaborate application framework.

# SCENE GENERATION

Provide obvious Editor commands such as:

Game Tools > Scenes > Build All Scenes
Game Tools > Scenes > Build Bootstrap
Game Tools > Scenes > Build Gameplay

The scene generator must construct and configure everything necessary for the initial test.

This includes:

* camera
* lighting
* four-lane playfield
* judgement line
* note presentation
* runtime controllers
* input
* test chart
* UI
* audio objects
* serialized dependencies

Do not require manual Inspector assignment after scene generation.

If the scene generator discovers that something required cannot be produced safely, fail loudly with a useful error rather than leaving a partially configured scene.

# VISUAL PRESENTATION

Create a simple but clearly 3D presentation.

The player must immediately perceive:

* four lanes
* the judgement line
* incoming notes
* which lane each note belongs to
* successful / failed input feedback

Use simple primitives, basic materials, lighting, and perspective where useful.

Do not spend significant effort on final art.

The playfield may visually resemble a perspective rhythm-game highway.

For example:

camera

\    incoming notes
\   ■   ■
\      ■   ■
____________
judgement line
D F J K

This is merely presentation.

The simulation itself remains lane + time based.

# NOTE DATA

Create a small runtime representation that supports tap and hold notes.

Conceptually it should contain only information actually required by gameplay, such as:

* lane
* note kind
* start time
* end time for holds

Avoid embedding presentation state into immutable chart data.

Do not store GameObject references inside the fundamental chart definition.

The chart model must make sense even if no graphics exist.

Create a tiny deterministic test chart.

The chart should contain enough notes to verify:

* every lane
* isolated tap notes
* hold notes
* input in other lanes while a hold is active
* closely spaced notes
* simultaneous notes/chords if the current input implementation naturally supports them

Avoid overlapping hold notes in the same lane in this first chart. Validate chart data and fail clearly if a hold ends at or before it starts.

Keep it short.

This exists for verification, not content production.

If you create a ScriptableObject or other Unity asset for the test chart, generate/configure it from Editor tooling rather than requiring manual Inspector editing.

# NOTE LIFECYCLE

Separate note data from note presentation.

A reasonable conceptual separation is:

Chart / NoteData
↓
RhythmGameController
↓
NoteView

NoteView renders a note.

NoteView must not make authoritative hit/miss decisions.

The gameplay controller determines note states from:

* song time
* lane press and release input
* judgment windows
* chart data

and tells presentation what happened.

For hold notes, use an explicit small lifecycle such as:

Pending -> Holding -> Completed

with Missed as a terminal alternative. The controller owns these transitions. A successful head press records its Perfect or Good grade and enters Holding. Releasing within the configured end window records a tail grade and completes the note; use the worse of the head and tail grades as the hold's single final judgment. Releasing too early misses it. If the lane remains held beyond the late end window, complete the hold using its recorded head grade so keyboard polling order or a late physical release cannot strand the note. Count a hold once, when it reaches Completed or Missed, and do not add periodic hold ticks.

Avoid spawning and destroying large amounts of unnecessary garbage if a simple implementation can avoid it, but do not build an elaborate object pool yet.

# INPUT

Use Unity's currently supported input workflow if it is already available in the project.

Do not build a custom input framework.

Represent gameplay input semantically as four lane presses and releases.

The gameplay system should ultimately care about events equivalent to:

PressLane(0)
PressLane(1)
PressLane(2)
PressLane(3)

ReleaseLane(0)
ReleaseLane(1)
ReleaseLane(2)
ReleaseLane(3)

rather than knowing arbitrary keyboard details everywhere.

Default keyboard bindings:

Lane 0 = D
Lane 1 = F
Lane 2 = J
Lane 3 = K

Keep actual input bindings separable from rhythm judgment logic.
Develop controls and input bindings modularly so each lane/key binding can be added or changed independently.

A key press or release timestamp must be compared against song time. The input layer must also expose whether each lane is currently held, without making keyboard APIs part of judgment logic.

Do not detect hits by raycasting or physical contact with notes.

# JUDGMENT

Implement a deliberately small judgment model.

For example:

* Perfect
* Good
* Miss

Judgment windows must be configurable data rather than unexplained magic numbers scattered through code.

Use the same named Perfect and Good windows for tap timing, hold-start timing, and hold-release timing unless there is a clear reason to serialize separate start/release values. An early release before the allowed release window is a Miss. A hold that was never started becomes a Miss once its start window expires. A hold completion produces one final judgment; do not increment result counts at both its head and tail, award score every frame, or invent hold ticks.

Use absolute timing error:

timingError = inputSongTime - targetTime

For `targetTime`, use the tap time, hold start time, or hold end time appropriate to the event being judged.

and judge based on abs(timingError).

Preserve the signed timing error internally where useful so early/late behavior can later be inspected.

Do not base judgment on:

* note Transform position
* renderer bounds
* collider overlap
* frame number

A missed note should occur when song time passes beyond its allowed hit window.

Again, this must be determined from time, not from the note crossing a world-space trigger.

# NOTE MATCHING

When a lane press occurs:

* look at eligible unresolved notes in that lane
* identify the appropriate candidate based on timing
* resolve a tap immediately, or begin a hold
* prevent the same note from being judged twice

When a lane release occurs:

* inspect the active hold in that lane
* compare release song time with its end time
* complete or miss it according to the explicit release rule
* prevent a completed or missed hold from being resolved again

Keep this logic deterministic and readable.

Do not scan arbitrary scene GameObjects looking for notes.

Gameplay state should already know which chart notes exist.

# 3D NOTE POSITIONING

The 3D note renderer should compute its position from song time.

Conceptually:

remainingTime = note.startTime - songTime
position = judgementPosition + travelDirection * remainingTime * scrollSpeed

This means rendering remains naturally synchronized even if a frame is delayed.

Do NOT implement note travel using something like:

transform.position += velocity * Time.deltaTime

as the authoritative movement model.

A temporary frame hitch should produce a visual jump to the correct musical position on the next frame rather than permanently shifting note timing.

Render a hold note with a head and a body whose length represents `(endTime - startTime) * scrollSpeed`. While it is actively held, pin or clip the head at the judgment line and derive the remaining body/tail position from current song time. Do not shrink the body by accumulating `deltaTime`.

# AUDIO

Add a short test audio clip if a suitable existing project asset is available.

If no suitable audio asset exists, do not silently download copyrighted music.

You may create the timing foundation and use an appropriate project-owned or generated test sound if practical.

The architecture must support:

* scheduling playback
* establishing song-time zero
* querying current song time

Do not make AudioSource.time the only fundamental timing abstraction if a more precise DSP-clock-based relationship is appropriate.

Keep offset handling explicit.

Do not implement an elaborate audio engine.

# GAME SESSION

Implement only enough state to support:

Ready
Playing
Completed

or an equally small equivalent.

The player must be able to:

1. start the test chart
2. play through it
3. receive judgments
4. reach the end
5. restart

Do not create a giant generic state-machine framework.

# SCORE / RESULTS

For this first foundation, show only useful verification information such as:

* score or resolved-note count
* combo
* Perfect count
* Good count
* Miss count
* current song time if useful for debugging

Keep the scoring formula simple.

Do not attempt to reproduce osu!, DJMAX, StepMania, Quaver, or another game's exact scoring system unless explicitly requested.

The purpose of this implementation is to prove the architecture and gameplay loop.

# UI

Generate minimal runtime UI.

It should show:

* combo
* recent judgment
* basic result counts
* completion state
* restart control or instruction

UI scripts must be normal runtime scripts.

They must not use UnityEditor.

# DEBUGGING

Errors must not fail silently.

Use clear assertions or Debug.LogError where broken configuration would otherwise cause confusing behavior.

Examples:

* chart missing
* invalid lane index
* SongClock missing
* AudioSource missing
* malformed judgment windows

Do not log every frame.

For rhythm diagnostics, it is acceptable to expose concise information such as:

* signed timing error
* current song time
* scheduled DSP start time

where useful.

# CODE QUALITY

Favor boring, explicit, readable C#.

Rules:

* one clear responsibility per component
* prefer composition over giant controller classes
* use private serialized fields when Unity serialization is appropriate
* avoid public mutable fields
* avoid runtime FindObjectOfType-style dependency discovery when the scene generator can wire the reference explicitly
* do not use arbitrary GameObject names as runtime identifiers
* avoid unnecessary Update methods
* avoid allocations in obvious per-frame timing paths
* no global service locator
* no generic event bus merely for decoupling
* no premature ECS
* no custom dependency-injection framework
* no empty scaffolding classes for imagined future features

Use current Unity 6.3 APIs.

Avoid deprecated APIs.

Editor-specific APIs belong only in Editor code.

# IMPORTANT RHYTHM-GAME INVARIANTS

Maintain these invariants throughout the project:

1. Song time determines rhythm state.

2. Chart data determines when notes should be hit.

3. Lane identity is discrete gameplay data.

4. Hold duration is chart data, not a property inferred from presentation.

5. Transform position does not determine judgment.

6. Physics does not determine judgment.

7. Note visuals are derived from gameplay time.

8. A dropped rendering frame must not permanently alter note timing.

9. Runtime gameplay must continue to make sense independently from the Unity Editor.

10. Scene generation belongs to Editor tooling.

11. Generated scene files are products of the C# scene builders.

# SCENE GENERATOR VERIFICATION

Verify generation itself.

Perform:

1. Delete or move aside the generated Bootstrap and Gameplay scenes.
2. Run the scene-generation command.
3. Confirm both scenes are recreated.
4. Confirm references are assigned.
5. Confirm the four lanes are created.
6. Confirm the judgement line exists.
7. Confirm the chart and runtime controllers are connected.
8. Run generation again.
9. Verify duplicate objects are not accumulated.
10. Verify the scenes remain valid.

If something is wrong, fix the generator.

Do NOT manually repair the generated scene and leave the generator incorrect.

# GAMEPLAY VERIFICATION

Actually test the game.

Do not stop at compilation.

Perform:

1. Compile with zero C# errors.
2. Inspect Console.
3. Open the generated startup scene.
4. Enter Play Mode.
5. Start the test chart.
6. Verify audio/song clock starts correctly.
7. Verify notes visually approach the judgement line.
8. Verify the notes remain synchronized with song time.
9. Press all four lane inputs.
10. Intentionally hit tap notes early.
11. Intentionally hit tap notes late.
12. Start and successfully complete hold notes.
13. Press other lanes while a hold remains active.
14. Release a hold within its release window.
15. Release a hold too early and verify Miss.
16. Let a held note pass its end window and verify deterministic automatic completion.
17. Intentionally miss tap notes and hold starts.
18. Verify Perfect / Good / Miss classification.
19. Verify a note cannot be judged twice.
20. Verify misses occur based on time.
21. Verify combo behavior.
22. Complete the chart.
23. Verify results.
24. Restart.
25. Verify all held-input and note states reset correctly.
26. Exit Play Mode.
27. Inspect Console again.

If practical, introduce a temporary frame-rate reduction or otherwise observe behavior under irregular frames and confirm that note position derives from current song time rather than accumulated frame movement.

# RUNTIME / EDITOR BOUNDARY VERIFICATION

Verify:

* Runtime source contains no UnityEditor dependencies.
* Editor tooling lives in Editor compilation context.
* Runtime assemblies do not reference Editor assemblies.
* Gameplay still works after the generated scenes already exist without Editor code participating in runtime.
* A standalone player build succeeds if the environment allows it.

Play Mode alone is not sufficient proof of correct runtime/editor separation.

# FINAL CLEANUP

Before finishing:

* remove abandoned experimental scripts
* remove unused GameObjects
* remove unused assets generated during failed attempts
* save generated scenes
* use meaningful GameObject names
* ensure no Missing Script state exists
* ensure serialized references are valid
* ensure runtime code contains no Editor-only API use
* ensure scene builder code remains capable of recreating the project scenes
* do not leave Unity in Play Mode

Finally report:

1. What you created
2. Scene-generation commands
3. Editor scripts and responsibilities
4. Runtime scripts and responsibilities
5. Generated scene hierarchy
6. Exact rhythm-game timing architecture
7. How SongClock works
8. How note position is calculated
9. How judgment is calculated
10. How hold start, sustain, release, early release, and automatic completion work
11. Test chart structure
12. What you actually tested in Play Mode
13. Whether scenes were successfully regenerated from scratch
14. Whether repeated generation remained clean
15. Whether runtime code was verified independent of UnityEditor
16. Any limitations or known problems
17. The next THREE sensible development steps

Important behavioral rules:

When faced with:

A. moving notes using accumulated transform motion

or

B. deriving note transforms from authoritative song time

choose B.

When faced with:

A. determining hits using Unity physics

or

B. determining hits mathematically from lane + timing error

choose B.

When faced with:

A. manually fixing generated scene contents

or

B. fixing the scene builder

choose B.

When faced with:

A. putting Editor behavior inside runtime gameplay code

or

B. performing Editor work during scene/content generation and serializing ordinary runtime-compatible results

choose B.

When faced with:

A. building infrastructure for imagined future rhythm-game features

or

B. implementing the smallest clean system required for four-key tap-and-hold gameplay

choose B.

The generated scenes are products.
The Editor C# scene builders are their source definitions.

The 3D scene is presentation.
The rhythm game is lane + time.

Build a strong playable foundation, verify it, then stop.
