# Selection record columns — 2026-10-31

- Score/rank/rate occupy the left group at 34–40 reference pixels. Maximum combo and history occupy the right group at 32–36, with larger 26-pixel completion details and a 30-pixel history caption. Warm divider and shared bold Mona font match the existing panel.
- Compilation passed. SongSelection was regenerated from its layout builder; other scenes were not regenerated for this change.
- SelectionRecordColumnsTests: 3 passed at logical 1920×1080, 1728×1080 and 1440×1080. Checks separate bounds, shared font, and text-generation height for maximum score/combo plus clear/challenge/failed history. The initial 4:3 overflow was corrected by widening the history column and tightening its line spacing.
- CompletedRunSavesBestAndAbortedRunDoesNotCreateRecord: 1 passed, including actual score on the left and combo/history on the right after returning from gameplay.
- FavoritesEmptyStateAndSortingUseSongIds: 1 passed. Empty selections reset both groups; no stale selected record remains in the source path.
- Total final related tests: 5 passed. Editor returned to ready/stopped; modified source whitespace checks passed.
- One queued rerun was interrupted by the scene-save asset refresh; it was started again after the Editor was ready. Only completed runs above are counted.
- No captures, screenshots, player builds, full-project tests or unrelated smoke runs were performed. Capture-producing browser tests were excluded.
