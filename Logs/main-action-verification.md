# Main branch action verification — 2026-10-01

Backported the shared verification changes from `92add5e` onto main (`1ff8a42`) without the title-screen feature.

Included: action-based PlayMode fixtures, UI/Gameplay/Authoring category menus, the Foundation Test Runner orchestrator, startup restoration and Test Runner cleanup handling, four-scene regeneration coverage, and the verification guide.

Excluded: Title scene and its tests, fonts, main-menu runtime code, title startup behavior, Bootstrap camera changes, and scene/build-settings changes. The existing Bootstrap entry point is preserved. `TestRunStartup` is a test-only helper that does not reference title-screen types.

Validation with Unity 6000.3.23f1 on Windows:

- `RhythmDojo.EditorTools.FoundationVerification.RunAll`: **100 EditMode tests passed**, **24 PlayMode tests passed**, zero failures, skips or inconclusive tests.
- **Windows x64 player build succeeded**, starting at Bootstrap.
- Test Runner cleanup completed with **zero `Assets/InitTestScene*` files** remaining.
- `git diff --check` passed.

| PlayMode fixture | Passed |
| --- | ---: |
| SongSelectionFlowTests | 7 |
| SettingsFlowTests | 3 |
| GameplayHudFlowTests | 3 |
| GameplayFlowTests | 2 |
| GameplayJudgmentTests | 5 |
| AudioFlowTests | 1 |
| AuthoringFlowTests | 3 |

Local raw evidence (ignored by Git): `Logs/main-foundation.log`, `Logs/verification.txt`, `Logs/verification-editmode.xml`, and `Logs/verification-playmode.xml`. The player is `Builds/Windows/RhythmDojo.exe`. Scene serialization and automatic settings migrations from running Unity were reverted after validation.

Build success verifies compilation and packaging, not a manual player smoke test. The existing editor project on `ui/yunjang-titlescene` and its local scene modifications were not changed by this backport.

See [the action verification guide](../Docs/verification.md) for button expectations and execution commands.
