# Kim/editor main update — 2026-10-23

- Integrated origin/main at d81476b into Kim/editor, retaining both branch histories.
- Used the latest main waveform, serialized timeline View, scene composition and v3 document implementation to resolve integration conflicts.
- Per user selection, Grid Correction uses the playback cursor and the Advanced GRID control is available. Retained the Kim/editor fix that applies the entered BPM together with the origin in one undoable edit, and adapted its regression test to cursor-based origin selection.
- EditMode: 58/58 passed (BeatGridEditingTests, WaveformPeaksTests, SongProjectTests). Results: Logs/main-update-editmode.xml.
- PlayMode: 18/18 passed (AudioTimelineTests, ChartEditorScreenTests, ChartEditorMenuTests, ChartProjectionRenderingTests, plus the four navigation/input/cleanup Smoke methods documented in Docs/verification.md). Results: Logs/main-update-playmode.xml.
- This is affected-scope integration verification, not a full test run. No player build was requested or performed.
- Working scene/settings/material snapshots were taken before tests. Test-generated changes were removed after the runs.
- Pre-update local changes are preserved in stash af807b9944fa780038cbaa2bbbfa651e394a8de8 and as raw files under Logs/local-update-backup-20261023-215048. Incompatible local scene changes remain available there if restoration conflicts with updated scenes.
