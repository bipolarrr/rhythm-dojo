# Song Selection brush typography — 2026-10-31

- Applied unmodified Google Fonts Dokdo-Regular.ttf in bold to all saved Song Selection Text components, including hidden dialogs, row templates and dropdown options. UiFontScope preserves that face for dynamic control factories and cloned rows; the shared Mona default remains intact.
- Font metadata embeds the font data and persists the Mona fallback reference. The OFL license is provided alongside the font and in Resources/UI for inclusion with the player. Source: https://github.com/google/fonts/tree/main/ofl/dokdo
- Compilation passed. SongSelection was saved through its authoritative builder. Font fallback settings were verified in the saved .meta file after the interrupted session resumed.
- UiTypographyTests: 8 passed (six saved scenes, shared popup/runtime controls, and scoped controls with Korean/digit support and fallback checks).
- SelectionRecordColumnsTests: 3 passed at 16:9, 16:10 and 4:3, including full score/combo/clear/challenge/failed history at the configured minimum text sizes using the new font.
- Verify Smoke Actions: 4 passed, 0 failed, 0 skipped, 0 inconclusive. Logs/verification.txt reports PASS for the run started 2026-10-30T20:54:33Z.
- Final total: 15 related tests passed. Modified source whitespace checks passed. No screenshots, captures, player builds or full-project test suite were run. The existing title-logo asset was inspected directly without capturing the application.
