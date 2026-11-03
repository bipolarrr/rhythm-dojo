# Live editor gameplay preview

The right-hand help/next-chart area is now a live 3D lane preview. It reuses the gameplay PlayfieldPresenter, tap/hold prefabs, presentation materials/settings, and constant-scroll timeline at 1x. It displays the active unsaved chart at the editor audio time plus its audio offset. It previews notes without running gameplay scoring or creating another audio player.

- Preview follows playback, seek/rewind, edits/undo, and chart-slot selection.
- A currently active hold is shown at the judgement line. Passed notes disappear.
- Separate tap/hold pools reuse visible models. The render texture tracks the preview's on-screen size, capped at 1024 pixels per dimension.
- Preview geometry uses a dedicated camera layer excluded from the editor display camera. Its light only affects that layer; it has no AudioListener.
- Leaving the screen removes the preview camera/world and releases its texture; the original display-camera mask is restored.
- WorkspaceSceneBuilder creates the lasting layout and references. The saved scene was updated without regenerating unrelated scenes. Selection/tool information remains below the preview.

## Verification

Unity 6000.3.23f1, isolated project copy, sequential PlayMode runs.

Feature and appearance checks: 2 passed, 0 failed.

- GameplayPreviewFollowsAudioEditsAndChartSelectionAndReleasesTexture: Saiki audio, 128 BPM, tap/hold rendering and image readback, seek, live playback, edit/undo, chart switching, no duplicate AudioListener, camera isolation, and texture/camera cleanup on exit.
- EditorUsesDefaultPresentationAndRestoresThemedPointerOnTitle: existing editor appearance and cross-scene cursor regression.

Repository Smoke scope: 4 passed, 0 failed.

- MainMenuFlowTests.StartLoadsBootstrapThenExistingSongSelection
- SongSelectionFlowTests.MouseClickUsesUiInputModuleToLoadSong
- GameplayFlowTests.KeyboardNavigationAndStartRestartFocusLossUseActualActions
- GameplayHudFlowTests.ReturnButtonInReadyStateLoadsSongSelectionAndReleasesSong

Visual evidence: Logs/editor-gameplay-preview.png, read from the actual runtime preview texture and inspected. Results: Logs/editor-gameplay-preview-playmode.xml and Logs/editor-gameplay-preview-smoke.xml with their corresponding logs (ignored artifacts). Changed source/test files pass git diff --check. No full suite or player build: no gameplay contracts or build settings changed.
