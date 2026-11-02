# Key settings verification

- Added 키 셋팅 below 게임플레이 in Settings and the shared SettingsPopup prefab.
- Default lane keys: D / F / J / K. Keys save immediately in PlayerPrefs and update active gameplay input and HUD labels through the same change notification.
- Duplicate keys and reserved Escape/Space keys are rejected. Escape cancels capture. Reset restores all four defaults.
- Navigation events remain suspended until the captured key is released, including Enter and arrow keys.
- Unity 6000.3.23f1 compilation succeeded.
- EditMode: PlayKeyBindingsTests, 7 passed / 0 failed. Checks invalid storage, default recovery, reserved keys, and duplicate rejection.
- PlayMode: 8 passed / 0 failed. Includes both KeySettingsFlowTests, the existing gameplay-settings and popup-reuse checks, and all four required Smoke actions.
- PlayMode coverage includes reopening settings after saving, cancellation without closing the popup, Enter/arrow key capture, reset, all four custom lane inputs, old-key rejection, and immediate HUD/input updates after rebinding during pause.
- Tests ran in an isolated project copy because the original Editor's main-thread commands timed out while starting a test run. Source Assets, Packages, and ProjectSettings were copied without modifying the original Editor session.
- No player build was run. Newly added screenshot calls were removed at the user's request after the successful test run; no further capture or build was performed.
- Results: key-settings-editmode.xml and key-settings-playmode.xml.
