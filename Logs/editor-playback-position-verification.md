# Grid playback-position tool

The previously added I----I tool now sets the preview playhead instead of scrolling. Its Tools menu caption is 재생 위치; its toolbar location and icon stay beneath Grab. Existing serialized action and icon enum values are preserved.

- Click a grid position to move the white playhead there. Dragging adjusts the position too.
- Use the editor's note quantization when snapping is enabled.
- Convert the projected beat through the tempo map and subtract the audio offset to seek the music.
- Preserve the viewport when positioning the playhead. Space starts playback from the chosen position.
- Leave notes and selection unchanged. Existing wheel scrolling remains available.

## Verification

Unity 6000.3.23f1, isolated copy of the current working project, PlayMode: 3 passed, 0 failed.

- PlaybackPositionToolSeeksClickedGridLineWithoutEditingNotes: LocalAudio/Saiki.wav, tempo map 128 -> 256 -> 64 BPM and 250 ms audio offset. Checks menu caption and selection, grid clicks, white playhead, audio sample playback from the chosen position, stable viewport/zoom while paused, snapping, dragging, negative time, unchanged notes and selection.
- ControlSpaceReturnsToPreviewStartAndOrdinarySpaceStillPauses: playback-return shortcut regression.
- LeftToolIconsSelectDrawEraseAndGrabWithVisibleSelection: existing tool regression.

Evidence: Logs/editor-playback-position-playmode.xml and .log (ignored artifacts). Changed source and test files pass git diff --check. No full suite or player build: the change is limited to editor preview positioning.
