# Scene regeneration test cleanup

Base: 677e842. The root previously observed uncommitted Results.unity and ChartEditor.unity output after task6 and whole-suite regeneration and restored those outputs manually. Static tracing confirms SceneBuilder.BuildAll writes seven scenes, while AllFiveScenesCanBeRegeneratedWithoutReplacingTheirGuids backed up only five.

The existing test name remains unchanged. Its scene path set now includes Results and ChartEditor, so both are backed up/restored with their .meta files and receive the same regeneration/GUID/scene checks. ScenePresentationBuilder.Settings also unconditionally saves Assets/Game/Resources/UI/SettingsPopup.prefab. That known output and its .meta are included in the backup only (not deleted or opened as scenes). Build settings restoration remains unchanged.

Artwork loader calls in scene builders use existing sprites without the refresh option; gameplay artwork properties require existing assets. No additional unconditional artwork rewrite was found on BuildAll's path. No product builder or generated asset is changed.

Root will run SceneRegenerationTests and compare scene/popup hashes before final Foundation. This agent did not execute Unity or commit.
Root validation: SceneRegenerationTests 2/2 passed after refresh/recompile. Before/after SHA256 matched for all 16 scene/prefab/meta files; no generated asset changes remained. Editor Play Mode=false, compiling=false, ready. Independent read-only review found no blockers. Existing test name retained; GUID/validation assertions expanded rather than weakened.
