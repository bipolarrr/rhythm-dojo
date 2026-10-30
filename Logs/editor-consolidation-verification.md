# Consolidated chart editor

Replaced the old visible editor interface with the current workspace interface at Assets/Game/Scenes/ChartEditor.unity. WorkspaceEditor.ScenePath aliases ChartEditorMenu.ScenePath. Removed the separately generated ChartWorkspace scene and its Build Settings entry. Existing song/resource store and data formats are unchanged.

ChartEditorSceneBuilder.Build delegates to WorkspaceSceneBuilder.Build; the previous foundation generation is now internal BuildBase. WorkspaceSceneBuilder always generates a fresh foundation before decorating it, avoiding duplicate components on regeneration. Build All Scenes calls the combined builder once. Both existing builder menu commands were exercised; each generated the canonical scene with exactly one WorkspaceEditor component and one Build Settings registration.

Title now has one 채보 에디터 button. The extra button is no longer generated; optional legacy view/action fields and methods remain compatible. ShowChartWorkspace forwards to ShowChartEditor. Updated UI subscriptions and validation for the absent optional button. Renamed visible workspace help/status text to 채보 에디터.

Verification:
- Compilation succeeded; regenerated ChartEditor and Title.
- MainMenuTests: 5 passed.
- WorkspaceEditorTests: initial 33/34 passed. The realtime auto-playtest combo assertion failed; rerunning that method without runtime changes passed. The initial fixture is not described as a clean full pass.
- ChartEditorMenuTests: initial 4/5 passed. Its playtest return assertion assumed a Standard initial chart and three slots. Updated the expected slots to Easy/Hard for the new Easy default; reran the method successfully. Scoped the legacy adapter control lookup to exclude new popup controls with duplicate names.
- ChartEditorScreenTests: 4 passed.
- MainMenuFlowTests: 14 passed, including single editor entry and canonical workspace loading.
- Verify Smoke Actions: 4 passed, 0 failed. Evidence copied to editor-consolidation-smoke.xml.
- Visually inspected editor-consolidation-title.png and editor-consolidation-editor.png: one entry, canonical scene, current tools/menus and no visible 확장 name.
- Confirmed retired scene absent and regeneration does not duplicate editor components. Editor left open on the canonical scene in edit mode.

No full suite or Windows player build: scoped to editor scene composition and title navigation, with the required related checks and Smoke verification.
