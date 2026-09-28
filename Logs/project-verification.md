# Editor core and chart format v1 verification

Date: 2026-09-28. Unity 6000.3.23f1, Windows x64.

- Edit Mode: **99 passed, 0 failed** (71 existing + 28 project authoring cases). Evidence: `project-editmode.xml`, `project-editmode.log`.
- Play Mode: **10 passed, 0 failed**. Evidence: `project-playmode.xml`, `project-playmode.log`.
- Windows player: **Build Finished, Result: Success**; Unity exited with code 0. Evidence: `project-build.log`. Output: `Builds/Windows/RhythmDojo.exe`.
- Both JSON examples passed PowerShell `Test-Json -SchemaFile Docs/rdchart-v1.schema.json`; both also loaded through the runtime parser in Edit Mode.
- Newtonsoft package resolved to `com.unity.nuget.newtonsoft-json` **3.2.1** at dependency depth 0.

Authoring coverage includes detached snapshots, transactional batches, 100-command history, redo clearing, stable session note IDs, chart lifecycle, stable simultaneous-note order, double precision, Korean metadata, multiple charts, draft persistence, saved-content equality, edits during asynchronous save, failed save/load state preservation, cancelled save, replacement failure with the original file locked, concurrent stores, audio hashing/copy/retention, folder relocation, missing audio, invalid versions/types/fields/keys/IDs/paths, playback equivalence and structured note errors, and ScriptableObject copy isolation.

The existing `SettingsScreen.audio` CS0108 warning remains. No new compiler errors were reported. Editor UI, user-song registration and external audio decoding are intentionally deferred; this verification establishes the headless authoring/storage API and preserves the existing playback path.
