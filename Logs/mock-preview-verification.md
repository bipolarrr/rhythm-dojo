RhythmDojo development UI Mock verification — 2026-11-15
Branch: emergency. Unity 6000.3.23f1. Existing unrelated working changes preserved.

Behavior and usage
- Game Tools > Preview > Mock Controls opens the development window.
- One-click clear, failure, maximum score, all-Miss and pause presets.
- Score, accuracy, max combo, judgment counts, song labels, failure/completion and failure time can be edited. Live updates reuse the displayed screen; Apply replays its real entrance.
- Existing Results / Results (Game Over) menus remain. Chart review is available through the existing chart sample button, preserving hold maintenance misses, early release and unreached notes.
- Pause uses the authored Gameplay scene panel and DojoReveal. Mock Resume dismisses the panel, Restart replays its reveal, Exit stops the preview. These controls never start or resume a real song.
- Editor API: ResultsPreview.Begin(new ResultsPreviewData { score = 123456, accuracy = 87.65, maxCombo = 432 }); ResultsPreview.BeginPause(); ResultsPreview.Stop().

Investigated original call chain (current emergency worktree, not just main)
ResultsPreview.Begin -> Results playModeStartScene -> Play Mode/domain and scene reload -> ScreenCompositionRoot.Start -> AppFlowController.Create -> ResultsScreen.Initialize(null, actual return/preferences/calibration callbacks) -> wait two frames -> CreateResult -> ResultsScreen.Display -> Canvas.ForceUpdateCanvases.
AppFlowController.Create builds the song library/providers, preferences, rules, selection and audio services; PresentationPreferences.SaveAudio/ApplyGraphics also runs. These are unnecessary for this UI preview and connect real persistent settings/navigation.
CreateResult constructs a six-note chart, tempo, RhythmSession, Judged subscription, Start/PressLane/ReleaseLane/Advance sequence and a review snapshot. It simulates a ten-second timeline synchronously; it does NOT wait ten seconds in real time.
The original command already opens Results directly. There was no Title/Bootstrap/Gameplay detour and no actual song playback. The current emergency version also already allowed repeated clear/failure display in the same Play Mode. Those existing capabilities were retained.

Measurements (this machine; observed samples, not guaranteed latency)
- Original Editor.log: domain reload 5462 ms; scene operation 1153.697 ms. See before-startup.txt. These are individual logged stages, not a single instrumented end-to-end total.
- Original CreateResult: 10000 objects in 217.700 ms (~0.0218 ms each). Therefore simulated judgment work alone did not explain the seconds-long wait. The Mono allocation counter returned zero and is not treated as valid allocation evidence.
- After isolating game composition but retaining domain reload, the initial display dispatch was still 7112.195 ms (CLI observation), confirming the remaining startup cost.
- Final fast entry: 1042.765 ms until the loaded ResultsView was observed; internal display dispatch 989.637 ms. Real 2.02-second entrance animation is intentionally retained and is not included in this startup figure.
- 100 immediate data updates: internal display mean 0.131 ms, max 1.509 ms; same scene, ResultsView and child instance identities throughout.
- Alternating warm comparison on the same loaded real ResultsScreen, 10 warmups then 100 calls each: old CreateResult + Display + ForceUpdateCanvases mean 2.461 ms (median 2.413, max 3.063); full new Begin(data) mean 0.180 ms (median 0.172, max 0.266). About 13.7x less synchronous call time. This includes new serialization. It is not total frame/render time: normal canvas rebuild still occurs in Unity's render pass. See repeat-comparison.txt and Performance.cs.
- New summary creation: 10000 objects in 6.998 ms. Secondary optimization, not the main startup win.

New call chain
Editor controls -> summary values -> one authored scene load -> sceneLoaded disables composition before Start -> ResultsScreen.Initialize/Display (or GameplayPauseMenu.Display).
The editor host temporarily enables DisableDomainReload while forcing normal scene reload. It restores the exact original option bits/enabled flag and playModeStartScene on exit/cancellation. Compilation ends the preview before reload; quitting also restores options.
No AppFlow, gameplay session, song loader, persistent preference service or record writer is created. Repeated values update the cached view. Switching Results/Pause loads each scene once, then reuses it until exit. No UI copies, additional generated scenes/prefabs, DI or state framework were introduced. The original UI builders, animation, easing, sound and review logic remain authoritative.
The UI additions are passive data-display entry points. Existing normal calls still use the calculated PlayResult score and SessionSnapshot accuracy. The regression check verifies overrides do not mutate that result and do not leak into subsequent normal display.

Verification
- EditMode ResultsPreviewTests: 2 passed (0.36 s), retained review samples.
- EditMode PlayResultReviewTests: 10 passed (0.58 s), including extended existing repeated-display regression.
- PlayMode ResultsFlowTests: 10 passed (125.48 s), normal gameplay results, real completion, immutable reveal/sound, review, retry and navigation.
- PlayMode GameplayPauseFlowTests: 3 passed (19.91 s), real pause/countdown/resume/restart/exit and frozen session/settings behavior.
- PlayMode Smoke category: 5 passed (37.39 s), common navigation/input/cleanup.
- All selected cases passed; zero skips/inconclusive. This is focused verification, not a full-suite or player-build claim. Build omitted: no build/platform changes requested.
- Probe.cs exercised real clear/failure cues, cached-scene switching, 100 replacements, detail review, pause interactions, absence of AppFlow/session, unchanged six preference stores, and original editing setup/start/options restoration.
- EditorProbe.cs verified pause-first startup, natural focus on Mock Controls (no test focus provider), actual animation completion at 2.02 s, restoring pre-existing DisableSceneReload options, and canceled-entry cleanup.
- Manual compilation request during a live pause Mock stopped the preview and restored options/start scene; no compilation errors (recompile-result.json). Repeated pause click restarted actual DojoReveal at alpha zero.
- Source diff whitespace check passed. ProjectSettings has no content diff after restoration. Unity returns to Edit Mode/ready. Existing test screenshots were copied to captures and original evidence files restored from previous-artifacts.

Limitations
The first use still loads the authored scene and its serialized assets; first-use glyph/asset/render costs are not eliminated. Both cached scenes remain loaded only during the Mock. Preview is unavailable during ordinary Play Mode or Foundation verification. Script recompilation ends the Mock. Animated UI durations remain unchanged. No subjective audio quality or hardware-latency claim is made from these measurements.

Files
Assets/Game/Scripts/Editor/SceneGeneration/ResultsPreview.cs
Assets/Game/Scripts/Editor/SceneGeneration/ResultsPreviewWindow.cs (+ Unity meta)
Assets/Game/Scripts/Runtime/UI/ResultsScreen.cs
Assets/Game/Scripts/Runtime/UI/GameplayPauseMenu.cs
Assets/Game/Tests/EditMode/PlayResultReviewTests.cs
Docs/verification.md (preview workflow), Docs/ingame-presentation.md
