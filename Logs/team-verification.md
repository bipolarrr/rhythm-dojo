# Four-person boundary refactor verification

Date: 2026-09-28. Unity 6000.3.23f1, Windows x64.

## Automated checks

- Edit Mode: 71 passed, 0 failed (`team-editmode.xml`). Existing domain, coordinator, tempo, asset preservation/migration and scene validation tests passed.
- New Edit Mode coverage: asset/document equivalence and defensive snapshots; incomplete draft copy semantics; malformed metadata, tempo, lane and overlap diagnostics; delayed audio cancellation and exactly-once release; missing audio/unknown mode; ID-based selection through reorder and duplicate provider rejection; independent 4/5/6-lane HUD layouts; byte-for-byte content preservation through repeated scene generation.
- Play Mode: 10 passed, 0 failed (`team-playmode.xml`). Existing mouse/keyboard selection, settings, six-lane gameplay, focus loss, restart and two-song checks passed.
- New Play Mode coverage: unsaved document loading into actual Gameplay and returning the editor session ID; audio lease release on return; cancellation followed by retry; renamed UI objects with explicit View references; options captured before delayed loading.
- Foundation: all five full-song runs passed (`verification.txt`, `team-foundation.log`): 13/2/3 mixed judgment, restarted 18 Perfect, no-input 18 Miss, and both Constant/BPM Tempo Shift runs with 8 Perfect. Scene regeneration from missing scenes and repeated generation passed.
- Final player build: see `team-build.log`; Windows output is `Builds/Windows/RhythmDojo.exe`.

The final asynchronous option-capture guard was verified by the 10-test Play Mode run and final player rebuild after the full-song integration pass.

## Reproduction

Close the project Editor. Run these separately and wait for each Unity process to exit:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -runTests -testPlatform EditMode -testResults 'C:/Users/song/RhythmDojo/Logs/team-editmode.xml' -logFile 'C:/Users/song/RhythmDojo/Logs/team-editmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -runTests -testPlatform PlayMode -testResults 'C:/Users/song/RhythmDojo/Logs/team-playmode.xml' -logFile 'C:/Users/song/RhythmDojo/Logs/team-playmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo' -executeMethod RhythmDojo.EditorTools.FoundationVerification.RunAll -logFile 'C:/Users/song/RhythmDojo/Logs/team-foundation.log'
```

## Limits

The in-game authoring UI, disk format and external audio import UI are intentionally outside this change. Memory documents exercise the integration contract. Human audio latency and audible dropout quality were not measured by these tests.
