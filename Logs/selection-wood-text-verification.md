# Song selection wood lettering — 2026-10-31

Supporting labels now use light brown (#D6A873), warm brown outlines and a static, subtle wood grain shader inside the glyphs. Selected difficulty labels use a brighter warm tan. The failed-completion rich text also follows the brown palette. The saved row template passes the material to song rows and dropdown templates retain it when instantiated.

The footer shown in the user's reference and both song title labels are excluded. The apply script compared their serialized Text state before/after and verified exact preservation. Font references, layout and existing sizes are preserved for previously styled labels. The builder applies the same style on regeneration.

Verification:

- Unity compilation succeeded; the saved scene rendered with brown wood lettering. Visual evidence: `Logs/selection-footer-SongSelection.png`.
- Footer and song title serialized state checks passed in `Logs/ApplySelectionWoodTextStyle.cs`.
- Related PlayMode tests: 3 passed, 0 failed (difficulty selection, mode popup and favorites/sorting). XML: `Logs/verification-selectiontextstyle.xml`, start 2026-10-31 11:55:59Z.
- Source whitespace check passed. No player build or full-project test run; changes are limited to selection typography and runtime palette refresh.
