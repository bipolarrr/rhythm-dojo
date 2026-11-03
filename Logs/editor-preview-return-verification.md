# Editor preview return shortcut

- Ctrl+Space starts preview and remembers its start position. Space pauses and seeks back to that position.
- Ordinary Space retains pause/resume behavior. The waveform Play/Pause button also honors the remembered return position.
- Seeking, loading another song, and stopping at zero clear the return position. Changing preview speed preserves it.
- Added the action at the end of WorkspaceAction to preserve serialized enum values.

## Verification

Unity 6000.3.23f1, isolated project copied from the current working tree. The running user project was refreshed and kept open.

PlayMode: 3 passed, 0 failed:

- ControlSpaceReturnsToPreviewStartAndOrdinarySpaceStillPauses: LocalAudio/Saiki.wav, 128 BPM; positive and negative start positions, repeated use, ordinary pause, stop/reset.
- SpaceTogglesPlaybackButNotWhileTypingOrConfirming: existing playback, text entry, and modal guards.
- ShortcutKeyEventsResolveEveryCommandWithExactModifiers: all shortcut bindings including Ctrl+Space and right Control.

Results: Logs/space-return-playmode.xml and Logs/space-return-playmode.log (ignored artifacts).

No full suite or player build: change is confined to editor preview shortcuts. Diff whitespace check passed for the changed authoring source and test file.
