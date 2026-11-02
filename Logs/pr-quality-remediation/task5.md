# Task 5 — Mode-specific keyboard settings

Base: d556709. Root owns Unity execution, generated scenes, verification evidence and commits.

## Reproduction

The two new FourLaneModeKeepsItsOwnDefaultBindingAndLabel cases failed on the old implementation: an independent four-lane keyboard mode and a gamepad lane both became <Keyboard>/d. Root recorded 7 pass / 2 fail in task5-red.json.

## Implementation

- ModeStorageKey v2 persists per-ModeId lane overrides. Original v1 data remains untouched and migrates once only for FourLaneMode; existing or corrupt v2 storage takes precedence. Reset removes only the requested mode and suppresses re-import for the original default.
- Production callers carry original SongDocument.ModeId through PlayableSong, input, HUD and result labels. Library metadata supplies selected song/chart modes without loading audio. Unknown provider mode metadata disables key editing rather than inferring from lane count. Title uses catalog default; pause uses CurrentRequest.
- Legacy mode-less APIs retain their original four-key fallback only before composition can provide the original FourLaneMode asset. No product UI or builder uses that fallback to select another mode.
- Keyboard defaults and labels come from GameModeDefinition. Non-keyboard/aggregate lane bindings stay intact and are not editable. Command and default-lane conflicts use Input System control resolution and overlapping state bits (including ctrl/anyKey); unresolved keyboard commands without a keyboard conservatively reject rebinding.
- Settings builder creates a styled row template and masked scroll content; runtime rows match mode lane count. Existing View fields remain. Builder and edit-time HUD use asset defaults, never saved bindings. Global Changed notification remains compatible; input adapters leave held keys untouched when effective bindings did not change.

## Verification requested

Compile then PlayKeyBindingsTests (migration/isolation/reserved command variants/builder defaults). Rebuild only SceneBuilder.BuildSettings() and SaveOpenScenes. PlayMode KeySettingsFlowTests (five tests including six-row top/bottom captures in Logs/pr-quality-remediation/task5-six-lanes-*.png), affected Settings/GameplayFlow tests, Smoke as root selects. Source frozen for first compile/review. No agent Unity invocation.

## Review and verification notes
Initial root verification: PlayKeyBindingsTests 22/22, KeySettingsFlowTests 5/5, GameplayFlowTests 3/3 passed. Independent review found that test cleanup must preserve an originally present empty v2 preference, since presence suppresses legacy migration. Added separate HasKey capture/restoration in both fixtures and an empty-string corrupt-v2 case (23 EditMode cases).
The initial SettingsPopup filter passed reuse but failed SettingsPopupTabsSaveAudioAndApplySeparateGraphicsControls immediately after the graphics tab click (sound panel remained active). No production fix or assertion relaxation was made for this failure. Root's isolated retry passed 1/1; original pair rerun is pending to check order dependency. Keep task5-settings-popup-first.json and task5-settings-popup-retry.json as evidence. Source remains frozen.

## Final task verification

PlayKeyBindings 23/23, KeySettingsFlow 5/5, GameplayFlow 3/3, SettingsPopup pair retry 2/2, and Smoke 4/4 passed. The initial graphics click failure is preserved. Isolated and original-sequence retries passed without product changes; retain this intermittent observation for the final suite, rather than claiming a root-cause fix.

The independent review's HasKey finding is resolved, with no outstanding findings. Only the Settings scene and popup were regenerated; existing meta GUIDs are unchanged. The top/bottom six-lane captures were visually reviewed. Play Mode exit and Editor ready were confirmed. Full tests and Windows build remain scheduled for final integration.