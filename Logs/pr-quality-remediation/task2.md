# Task 2 — reload external audio after configuration reset

Source baseline: 82e5575. Parent controls Unity, results and commits.

## Reproduction

AudioRecoveryFlowTests generates a 30-second 48 kHz PCM WAV under the system temporary directory and loads it through the real ChartEditorCompositionRoot.LoadAudio and DocumentSongLoader. It enters an editor playtest, pauses, resets the audio device, then resumes through GameplayPauseMenu. No PUPA or personal file is used.

Parent observed task2-red.json: 1 failure because the original external clip was retained. The original clip after AudioSettings.Reset reported GetData=false and sample peak=0 (task2-reset-probe.txt; parent preserves baseline copy). This is reproduced lost audio data, not only a changed object reference.

## Implementation

SongAudioLease retains its original constructor and adds a reload provider overload. The real external loader supplies that provider. DocumentSongLoader transfers the lease into a PlayableSong overload ordered (document, mode, lease), avoiding ambiguity with old null AudioClip calls. PlayableSong owns generation checks, cancellation and both active/discarded leases. AudioChanged publishes the replacement before disposing the previous lease, allowing the source to detach first. Subscribers throwing leave the replacement owned but invalidated, so retry remains necessary.

GameplayCompositionRoot binds the clip replacement, owns its recovery cancellation lifetime, and prepares playback without exposing file paths to SongClock. Repeated reset retries yield to allow Exit. A separate IPlaybackReadinessClock contract guards coordinator input.StartRequested as well as direct Start/Resume/Restart, avoiding a controller-only bypass. SongClock replacement preserves paused position; DSP scheduling remains task 3.

GameplayPauseMenu keeps the pause panel and Exit clickable during recovery, disables Resume/Settings/Restart, shows a builder-authored status label, and retains pause/error on failed decoding. Retry uses Resume; cancellation, reopening, exit and destruction invalidate async UI completion. Both before countdown and after countdown preparation checks prevent a reset during countdown from advancing invalid playback.

## Verification to run

- AudioRecoveryFlowTests: real WAV loss/reload, consecutive actual resets, sample and mixer output, editor return disposal, file deletion/retry without clock advance, real UI Exit during delayed recovery, built-in clip preservation.
- AudioLeaseRecoveryTests: overlapping generations, late result release, cancellation/retry, disposal during recovery, borrowed lifetime, independent loader cancellation propagation.
- CoordinatorTests: readiness gate plus existing paused input regression.
- GameplayPauseFlowTests and AuthoringFlowTests: existing integration paths.
- Gameplay builder regeneration needed for recoveryStatus field, then related layout/reference and Smoke checks.

No Unity execution performed by the implementation agent. Runtime files and existing public names retained; no file moves or GUID changes.

Independent review found pre-initialization invalidation could block Space without exposing recovery UI. Initialize now routes an already invalidated song through the same reset handler after initialization/input setup. Added a real-loader pre-invalidated song regression. Built-in reset now also measures mixer output and persists task2-builtin-output.txt; external output is in task2-recovered-output.txt. Coordinator readiness regression includes input.StartRequested; an additional PlayMode test queues real Space before recovery.

## Executed results reported by main

- CoordinatorTests: 61/61 passed (task2-coordinator.json), including direct input.StartRequested readiness gate.
- AudioLeaseRecoveryTests: 5/5 passed (task2-lease.json).
- AudioRecoveryFlowTests: 6/6 passed (task2-flow.json).
- Recovered external mixer peak: 0.11652759; 48 kHz; 1,440,000 samples.
- Borrowed built-in mixer peak: 0.06047606; 44.1 kHz; 529,200 samples.
- Main confirmed PlayMode exited and Editor ready after these runs. Existing pause integration and Smoke checks are still pending at this note; no full-suite claim is made.

The independent reviewer requested an initialization-before-reset recovery route; the added regression passed. No production/test source edits after this verification.

Final scoped verification: GameplayPauseFlowTests 2/2; Smoke category 4/4. All runner cleanup completed (PlayMode false, Editor ready). Independent reviewer confirmed pre-init invalidation fix and no remaining blocker. Gameplay scene regenerated only. Whole suite deferred to the audio-contract work group / final Foundation as planned; no Windows build for this intermediate task.
