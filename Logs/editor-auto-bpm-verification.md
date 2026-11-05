# Automatic BPM analysis on audio import

The workspace audio-import action now waits for preview audio loading to finish, then runs the existing BPM estimator automatically. Importing through the settings path field or its import action follows this path. The result fills both synchronized BPM fields and retains the existing explicit Apply behavior for chart timing.

Analysis runs on a worker task as before. Failure to estimate BPM leaves the imported audio available and displays a message so the user can enter BPM manually. Cancellation follows the workspace lifetime token.

## Verification

Unity 6000.3.23f1, isolated project copy, PlayMode: 3 passed, 0 failed.

- ImportAudioAutomaticallyEstimatesBpmWithoutChangingSavedTiming: imports LocalAudio/Saiki.wav, confirms automatic estimate within 2 BPM of the supplied 128 BPM, synchronized fields, unchanged project timing until Apply, then successful application.
- SpaceTogglesPlaybackButNotWhileTypingOrConfirming: silent audio reports automatic-analysis failure but still imports and plays; existing typing/modal guards pass.
- PlaybackPositionToolSeeksClickedGridLineWithoutEditingNotes: playback positioning, offset, tempo changes, and audio preview remain functional after automatic analysis.

Results: Logs/editor-auto-bpm-playmode.xml and .log (ignored artifacts). Changed source/test files pass git diff --check. No full suite or build: the change only connects the existing estimator to workspace audio import.
