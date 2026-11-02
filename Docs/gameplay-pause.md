# Gameplay pause menu

During a playing session, Escape opens a parchment dialog with timber buttons for 설정, 재시작 and 나가기. The upper-left arrow closes the menu and displays 3, 2, 1 on a central wooden seal before resuming the same song. Escape during the countdown returns to the paused menu. Ready-state return and editor-playtest return paths remain intact.

The song clock, judgments and note presentation remain frozen while the menu, settings or countdown is open. Restart clears the current session and pending resume; exit uses GameplayCompositionRoot's existing deferred shutdown. Settings reuse the existing modal popup and return to the pause menu, preserving editor-playtest return information. Audio configuration changes during pause restore the saved audio sample position on resume.

GameplayHudLayoutBuilder owns the saved menu layout and references; GameplayPauseMenu controls the dialog and countdown. RhythmSessionCoordinator gates advancement, judgments and keyboard restarts while paused. IResumableSongClock extends the existing pause contract without changing existing callers. Released hold keys are reconciled at the frozen song time when resuming.

Results uses the same default cover and legacy-cover replacement rule as Song Selection. A real registered cover takes precedence. Both screens fill and centrally crop the artwork inside a masked frame. Selection headings and song titles use warm ink and a subtle pixel highlight with the shared bold Mona font.
