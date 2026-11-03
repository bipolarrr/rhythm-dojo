# Task 6 — Shared UI artwork construction and native sliced rendering

Base: 790e294. Root owns Unity execution, generated assets, main ledger and commits.

## Baseline evidence

DojoEntranceAssets and SettingsArtAssets repeat the same texture importer configuration and Sprite.Create/CreateAsset operations. Their only material policies differ: entrance caller chooses full rectangle or alpha crop, settings uses the full rectangle and 128-pixel borders. PixelDojoFrameGraphic.BuildArtwork manually builds a 3×3 UV mesh, applying color only to the middle quad. UiFontSettings.Apply and ApplyTo repeat the same scope lookup.

Before production changes, PixelDojoFrameTests.ArtworkAtNormalAndSmallSizesPreservesTintMaskAndClickTargets renders actual SettingsPanel/SettingsControl artwork at normal and small sizes, with white/red center tint, RectMask2D clipping and the procedural fallback. It checks decorative raycast behavior and finite geometry, and writes task6-frames-current.png. Root will archive this as the before image, then run the same fixture after replacement.

## Implementation

- UiArtworkAssets owns shared point-filtered importer configuration, full/alpha crop, sprite creation and GUID-preserving serialized refresh. Both existing loader APIs remain. Builder callers specify crop and borders; legacy entrance name handling remains only in its original compatibility overload. The distinct bilinear gameplay importer is unchanged.
- PixelDojoFrameGraphic retains the procedural path and its public fields. Its artwork center is a single quad and its first child is a native Sliced Image with fillCenter=false. The regions do not overlap, preserving translucent sample composition. The border child follows layer, maskability, material, enabled state, CanvasRenderer tint and owner destruction. Builders explicitly prepare layers, while runtime field/layout changes synchronize before rendering.
- Current artwork has uniform 128-pixel borders, retaining its exact geometry. For explicit asymmetric borders the largest source border maps to borderWidth and the other edges scale proportionally; zero borders draw only the full center. Both policies have tests.
- UiFontSettings shares the same scope-aware application method for a single Text and a whole hierarchy.

## Requested verification

Compile, then PixelDojoFrameTests (4 cases), UiArtworkAssetsTests (3 cases), and UiTypographyTests. Compare task6-frames-current.png with archived before image. Regenerate only Settings and Gameplay, saving open scenes afterward; Title builder policy edits do not alter its existing assets/layout. Run SettingsFlowTests.GameplayTabPreservesAudioAndGraphicsAndSavesTimingLimits for native corner geometry and live tint, plus affected pause/key flows and chosen layout/regeneration checks. The 24-pixel corner assertion is retained on the border Image mesh; center/border mesh counts reflect separate layers. Root owns all execution.

## Validation lifecycle correction

The first implementation passed four frame tests and produced a pixel-identical matrix, but root/reviewer observed Unity warnings when restoring a scene: Graphic.OnValidate called the SetAllDirty override, which created/positioned a child during CheckConsistency. The override is removed. Layer synchronization now runs in OnEnable, explicit builder preparation, and LateUpdate. A SerializedObject inspector regression verifies that OnValidate does not create children and repeated updates reuse one layer. Scene and popup builders force canvas layout and prepare artwork before saving. PixelDojoFrameTests now has five cases. The archived warning log remains evidence of the corrected issue; it is not ignored.

## Executed verification

Root ran frame baseline 1/1, initial frame 4/4, and final frame 5/5 including Inspector regression. Artwork helper 3/3, typography 9/9, scene regeneration 2/2, settings tab geometry/input 1/1, and gameplay pause flow 2/2 passed. Final 1200×700 RGB comparison against the original implementation has zero changed pixels. New logs after recompilation, scene restoration and regeneration contain zero validation/compilation warnings matching the original issue. Independent re-review found no outstanding findings.

Only Settings, Gameplay and the shared popup are included as generated changes. The regeneration test's unrelated Results/ChartEditor output was archived locally in task6-test-generated.patch and restored. Existing meta files are unchanged. Generated scenes may receive a later Editor dirty notification; root saved them after layout updates before testing. The tests exited Play Mode and Editor ready was confirmed. Full integration/player verification follows task 7.
