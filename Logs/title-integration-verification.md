# Shared title-screen integration — 2026-10-01

Integrated the title-screen branch (`92add5e`) with main's shared verification backport (`50c1d18`) so UI contributors can start from main.

- Resolved all five merge conflicts while retaining the Title entry scene, five-scene regeneration coverage, and title UI tests.
- Consolidated PlayMode startup setup/cleanup into `TestRunStartup`; removed the duplicate main-menu helper.
- Open Title now checks for unsaved work in every open scene, including after an interrupted test run.
- README and `Docs/team-ownership.md` identify `Title.unity`, the layout builder, View/icon files, regeneration commands, and the UI validation workflow.

Validation: Unity **6000.3.23f1**, Windows, isolated worktree.

- `FoundationVerification.RunAll`: **103 EditMode + 30 PlayMode passed**, zero failures, skips or inconclusive tests.
- **Windows x64 build succeeded**, with Title as the first scene.
- Raw local evidence: `Logs/title-integration.log`, `Logs/verification.txt`, `Logs/verification-editmode.xml`, and `Logs/verification-playmode.xml` (ignored by Git).

Unity-generated scene serialization and automatic settings migrations were reverted after validation. The original project folder's local scene changes are preserved. Player build success is not a manual player smoke test; no visual redesign is included here.
